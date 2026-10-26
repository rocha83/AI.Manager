using System.Text.Json;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge;
using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Bridge.Flow;
using Rochas.AI.Manager.Bridge.Hosting;
using Rochas.AI.Manager.Bridge.Transport;
using Rochas.AI.Manager.Bridge.Tools;

var options = BridgeHostOptions.Parse(args);
if (options.ShowHelp)
{
    Console.WriteLine(BridgeHostOptions.Usage);
    return 0;
}

var startedAt = DateTimeOffset.UtcNow;
var bridgeOptions = new BridgeOptions
{
    Thinking = options.Thinking,
    Temperature = options.Temperature,
    MaxTokens = options.MaxTokens,
    ContextTokens = options.ContextWindow,
    Harness = options.Harness,
    MainHarness = options.MainHarness,
    TailScope = options.TailScope,
    LegacyHarness = options.LegacyHarness,
    QwenStandard = options.QwenStandard,
    ToolStrategy = options.ToolStrategy,
    BreakerSoft = options.BreakerSoft,
    BreakerHard = options.BreakerHard,
    SamplingPassthrough = options.SamplingPassthrough,
    ToolShimMode = options.ToolShimMode,
    ToolShimMaxString = options.ToolShimMaxString,
    ToolShimMinCalls = options.ToolShimMinCalls
};

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(options.BaseUrl);
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    json.SerializerOptions.PropertyNameCaseInsensitive = true;
    json.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(swagger =>
{
    swagger.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Rochas.AI.Manager.Bridge.Api",
        Version = BridgeContractVersion.ContractVersion,
        Description = "Host REST do componente de transporte do AI.Manager: manifesto, dry-run, status, contrato e /v1/*."
    });
});
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(bridgeOptions);
builder.Services.AddSingleton(new BridgeProxyOptions
{
    UpstreamUrl = options.UpstreamUrl,
    ModelId = options.ModelId,
    ApiKey = options.ApiKey,
    Timeout = TimeSpan.FromMinutes(30)
});
builder.Services.AddSingleton(serviceProvider => new BridgeProxy(
    new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
    serviceProvider.GetRequiredService<BridgeProxyOptions>()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(ui =>
{
    ui.SwaggerEndpoint("/swagger/v1/swagger.json", "AI.Manager.Bridge.Api v1");
    ui.RoutePrefix = "swagger";
});

// Handshake publico; o restante exige bearer quando --api-key foi informado.
app.Use(async (context, next) =>
{
    if (string.IsNullOrWhiteSpace(options.ApiKey))
    {
        await next();
        return;
    }

    var path = context.Request.Path.Value ?? string.Empty;
    var isPublic = path.Equals("/api/status", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/contract", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/contract/schema", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase);

    if (isPublic)
    {
        await next();
        return;
    }

    var authorization = context.Request.Headers.Authorization.ToString();
    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        || !string.Equals(authorization["Bearer ".Length..].Trim(), options.ApiKey, StringComparison.Ordinal))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = "missing or invalid bearer token" });
        return;
    }

    await next();
});

// Contadores de transporte: nao alteram a resposta; alimentam /api/metrics.
app.Use(async (context, next) =>
{
    BridgeMetrics.Inc("http.requests");
    try
    {
        await next();
    }
    catch
    {
        BridgeMetrics.Inc("http.errors");
        throw;
    }
});

app.MapGet("/api/status", () => Results.Json(BridgeComponent.Status(options, startedAt)))
    .WithTags("Componente")
    .WithName("GetStatus");

app.MapGet("/health", () => Results.Json(BridgeComponent.Status(options, startedAt)))
    .WithTags("Componente")
    .WithName("Health");

app.MapGet("/api/contract", () => Results.Json(BridgeComponent.Info()))
    .WithTags("Componente")
    .WithName("GetContract");

app.MapGet("/api/contract/schema", () => Results.Json(ManifestSchema.Generate()))
    .WithTags("Componente")
    .WithName("GetManifestSchema");

app.MapPost("/api/manifest/validate", (BridgeManifest? manifest) =>
{
    if (manifest is null)
        return Results.BadRequest(new { error = "manifest required" });

    var result = BridgeManifestValidator.Validate(manifest);
    return Results.Json(result, statusCode: result.IsValid
        ? StatusCodes.Status200OK
        : StatusCodes.Status422UnprocessableEntity);
})
.WithTags("Manifesto")
.WithName("ValidateManifest");

app.MapPost("/api/manifest/dry-run", (BridgeManifest? manifest) =>
{
    if (manifest is null)
        return Results.BadRequest(new { error = "manifest required" });

    var validation = BridgeManifestValidator.Validate(manifest);
    if (!validation.IsValid)
        return Results.Json(validation, statusCode: StatusCodes.Status422UnprocessableEntity);

    var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions
    {
        WorkspaceRoot = options.ArtifactRoot,
        Model = options.ModelId,
        ExperimentId = manifest.Experiment.Id,
        PlanId = manifest.Experiment.Version,
        Suite = manifest.Stages.SelectMany(stage => stage.Steps)
            .Select(step => step.ScenarioSelector?.Suite)
            .FirstOrDefault(suite => !string.IsNullOrWhiteSpace(suite))
    });

    return Results.Json(dryRun);
})
.WithTags("Manifesto")
.WithName("DryRunManifest");

// ---------------------------------------------------------------------------
// F1 — paridade de artefatos do host: tools, endpoints e configuracao.
// ---------------------------------------------------------------------------

app.MapGet("/api/tools", () => Results.Json(new
{
    chat = ToolCatalog.Chat(),
    responses = ToolCatalog.Responses(),
    names = new[] { "shell", "read", "write", "edit", "grep", "glob", "fetch", "git", "subagent" }
}))
.WithTags("Tools")
.WithName("ListTools");

app.MapPost("/api/tools/execute", (BridgeToolRequest? request) =>
{
    if (request is null || string.IsNullOrWhiteSpace(request.Name))
        return Results.BadRequest(new { error = "name required" });

    string workspace = string.IsNullOrWhiteSpace(request.WorkspaceRoot)
        ? options.ArtifactRoot ?? Environment.CurrentDirectory
        : request.WorkspaceRoot!;
    if (!Path.IsPathRooted(workspace) || !Directory.Exists(workspace))
        return Results.BadRequest(new { error = $"workspace invalido: {workspace}" });

    var executor = new BridgeToolExecutor(
        workspace, options.UpstreamUrl, options.ApiKey ?? "", request.Role ?? "");
    return Results.Json(executor.Execute(request));
})
.WithTags("Tools")
.WithName("ExecuteTool");

app.MapGet("/api/harness", () => Results.Json(new
{
    main = MainHarness.Text,
    behavior = string.IsNullOrWhiteSpace(bridgeOptions.BehaviorTail) ? HarnessTails.Behavior : bridgeOptions.BehaviorTail,
    tools = string.IsNullOrWhiteSpace(bridgeOptions.ToolsTail) ? HarnessTails.Tools : bridgeOptions.ToolsTail,
    harness = bridgeOptions.Harness,
    mainHarness = bridgeOptions.MainHarness,
    tailScope = bridgeOptions.TailScope,
    legacy = bridgeOptions.LegacyHarness,
    qwenStandard = bridgeOptions.QwenStandard
}))
.WithTags("Componente")
.WithName("GetHarness");

app.MapGet("/api/metrics", () => Results.Json(new
{
    uptimeSec = (long)(DateTimeOffset.UtcNow - startedAt).TotalSeconds,
    startedAt,
    transport = BridgeMetrics.Snapshot(),
    tools = ToolMetrics.Snapshot()
}))
.WithTags("Componente")
.WithName("GetMetrics");

app.MapPost("/api/permission/check", (JsonElement body) =>
{
    string model = ReadString(body, "model") ?? "";
    string tool = ReadString(body, "tool") ?? "";
    string args = ReadString(body, "args") ?? "";
    if (string.IsNullOrWhiteSpace(tool))
        return Results.BadRequest(new { error = "tool required" });
    var (action, reason) = PermissionPolicy.Check(model, tool, args);
    return Results.Json(new { action, reason = reason ?? "" });
})
.WithTags("Componente")
.WithName("PermissionCheck");

app.MapPost("/api/translate", (JsonElement body) =>
{
    if (!TryParseObject(body, out var request))
        return Results.BadRequest(new { error = "json object required" });

    // Aceita { request: {...}, model: "..." } ou o proprio request Responses.
    var source = request["request"] is JsonObject inner ? inner : request;
    string model = request["model"]?.GetValue<string>()
        ?? source["model"]?.GetValue<string>()
        ?? options.ModelId;
    var converted = ResponsesToChat.Convert(source, bridgeOptions, model);
    return Results.Json(new
    {
        model,
        body = converted.Body,
        temperature = converted.Temperature,
        thinking = converted.Thinking,
        hadTools = converted.HadTools,
        announcedTools = converted.AnnouncedTools,
        droppedTools = converted.DroppedTools,
        patternHits = converted.PatternHits,
        breakerTrip = converted.BreakerTrip
    });
})
.WithTags("Protocolo")
.WithName("TranslateResponsesToChat");

app.MapPost("/api/sanitize", (JsonElement body) =>
{
    if (!TryParseObject(body, out var request))
        return Results.BadRequest(new { error = "json object required" });
    if (request["tools"] is not JsonNode tools)
        return Results.BadRequest(new { error = "tools required" });

    var hits = new List<string>();
    int patterns = ToolSanitizer.StripUnanchoredPatterns(tools, "$", hits);
    bool enabled = request["enabled"] is not JsonValue ev
        || !ev.TryGetValue<bool>(out bool flag) || flag;
    int execute = ToolSanitizer.StripExecuteTools(tools, enabled, "api", null);
    return Results.Json(new
    {
        tools,
        strippedPatterns = patterns,
        patternPaths = hits,
        strippedExecute = execute
    });
})
.WithTags("Protocolo")
.WithName("SanitizeTools");

app.MapPost("/api/convert", (JsonElement body) =>
{
    if (!TryParseObject(body, out var request))
        return Results.BadRequest(new { error = "json object required" });

    var chat = request["chat"] as JsonObject ?? new JsonObject();
    string model = request["model"]?.GetValue<string>() ?? options.ModelId;
    double temperature = request["temperature"]?.GetValue<double>() ?? bridgeOptions.Temperature;
    var converted = ChatToResponses.Convert(chat, model, temperature, bridgeOptions);
    return Results.Json(converted);
})
.WithTags("Protocolo")
.WithName("ConvertChatToResponses");

app.MapGet("/api/config", () => Results.Json(new
{
    host = new
    {
        options.Port,
        options.Listen,
        options.BaseUrl,
        options.UpstreamUrl,
        options.ModelId,
        options.IsGate,
        options.ArtifactRoot,
        options.ContextWindow,
        options.MaxTokens
    },
    engine = new BridgeOptionsContract
    {
        Thinking = bridgeOptions.Thinking,
        Temperature = bridgeOptions.Temperature,
        StreamPerDelta = bridgeOptions.StreamPerDelta,
        AntiRepeat = bridgeOptions.AntiRepeat,
        AntiRepeatRetry = bridgeOptions.AntiRepeatRetry,
        AntiRepeatNotice = bridgeOptions.AntiRepeatNotice,
        AntiRepeatWindow = bridgeOptions.AntiRepeatWindow,
        MaxTokens = bridgeOptions.MaxTokens,
        ToolOutputLimit = bridgeOptions.ToolOutputLimit,
        Harness = bridgeOptions.Harness,
        ToolsTail = bridgeOptions.ToolsTail,
        BehaviorTail = bridgeOptions.BehaviorTail,
        CoerceNarrated = bridgeOptions.ToolStrategy,
        ContextGuard = bridgeOptions.LegacyHarness,
        ContextTokens = bridgeOptions.ContextTokens,
        ToolShimMode = bridgeOptions.ToolShimMode,
        ToolShimMaxString = bridgeOptions.ToolShimMaxString,
        ToolShimMinCalls = bridgeOptions.ToolShimMinCalls
    }
}))
.WithTags("Componente")
.WithName("GetConfig");

// /v1/models: passthrough do upstream (o cliente descobre o modelo servido).
app.MapGet("/v1/models", async (CancellationToken ct) =>
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    var request = new HttpRequestMessage(
        HttpMethod.Get, options.UpstreamUrl.TrimEnd('/') + "/v1/models");
    if (!string.IsNullOrWhiteSpace(options.ApiKey))
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
    try
    {
        using var response = await http.SendAsync(request, ct);
        string raw = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            return Results.Json(
                new { error = $"upstream HTTP {(int)response.StatusCode}", upstream = raw },
                statusCode: StatusCodes.Status502BadGateway);
        return Results.Content(raw, "application/json");
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
    }
})
.WithTags("Protocolo")
.WithName("ListModels");

app.MapPost("/v1/responses", async (
    JsonElement body,
    HttpContext context,
    BridgeProxy proxy,
    BridgeOptions bridgeOptions,
    CancellationToken cancellationToken) =>
{
    if (!TryParseObject(body, out var request))
        return Results.BadRequest(new { error = "json object required" });
    var effective = bridgeOptions.ForClient(context.Request.Headers.UserAgent.ToString());

    var isStream = request["stream"] is JsonValue streamValue
        && streamValue.TryGetValue<bool>(out var streamRequested)
        && streamRequested;

    if (!isStream)
    {
        try
        {
            var result = await proxy.CompleteAsync(request, effective, cancellationToken);
            return Results.Json(result.Body);
        }
        catch (BridgeProxyException exception)
        {
            return Results.Json(
                new { error = exception.Message, upstream = exception.ResponseBody },
                statusCode: exception.StatusCode);
        }
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers["X-Accel-Buffering"] = "no";

    try
    {
        await foreach (var item in proxy.StreamAsync(request, effective, cancellationToken))
        {
            await context.Response.WriteAsync(
                $"event: {item.Type}\ndata: {item.Payload.ToJsonString(JsonOps.SerializerOptions)}\n\n",
                cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
    catch (BridgeProxyException exception)
    {
        var payload = new JsonObject
        {
            ["type"] = "error",
            ["error"] = new JsonObject
            {
                ["message"] = exception.Message,
                ["upstream"] = exception.ResponseBody
            }
        };
        await context.Response.WriteAsync(
            $"event: error\ndata: {payload.ToJsonString(JsonOps.SerializerOptions)}\n\n",
            cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }

    await context.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
    return Results.Empty;
})
.Accepts<JsonElement>("application/json")
.WithTags("Protocolo")
.WithName("CreateResponse");

app.MapPost("/v1/chat/completions", async (
    JsonElement body,
    HttpContext context,
    BridgeProxy proxy,
    BridgeOptions bridgeOptions,
    CancellationToken cancellationToken) =>
{
    if (!TryParseObject(body, out var request))
        return Results.BadRequest(new { error = "json object required" });
    var effective = bridgeOptions.ForClient(context.Request.Headers.UserAgent.ToString());

    var isStream = request["stream"] is JsonValue streamValue
        && streamValue.TryGetValue<bool>(out var streamRequested)
        && streamRequested;

    if (!isStream)
    {
        try
        {
            var result = await proxy.ForwardChatAsync(request, effective, cancellationToken);
            return Results.Json(result.Body);
        }
        catch (BridgeProxyException exception)
        {
            return Results.Json(
                new { error = exception.Message, upstream = exception.ResponseBody },
                statusCode: exception.StatusCode);
        }
    }

    context.Response.StatusCode = StatusCodes.Status200OK;
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers["X-Accel-Buffering"] = "no";

    try
    {
        await foreach (var line in proxy.ForwardChatStreamAsync(request, effective, cancellationToken))
        {
            await context.Response.WriteAsync(line + "\n", cancellationToken);
            await context.Response.Body.FlushAsync(cancellationToken);
        }
    }
    catch (BridgeProxyException exception)
    {
        await context.Response.WriteAsync(
            $"data: {{\"error\":{JsonSerializer.Serialize(exception.Message)}}}\n\n",
            cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }

    return Results.Empty;
})
.Accepts<JsonElement>("application/json")
.WithTags("Protocolo")
.WithName("CreateChatCompletion");

Console.WriteLine(
    $"{BridgeContractVersion.ComponentName} {BridgeComponent.Version} " +
    $"(contract {BridgeContractVersion.ContractVersion}, schema {BridgeContractVersion.SchemaVersion})");
Console.WriteLine(
    $"api {options.BaseUrl} | upstream {options.UpstreamUrl} | " +
    $"role {(options.IsGate ? "gate" : "mirror")} | swagger {options.BaseUrl}/swagger");
Console.WriteLine("endpoints: /api/status /health /api/contract /api/contract/schema /api/manifest/validate /api/manifest/dry-run /v1/responses /v1/chat/completions");

app.Run();
return 0;

static bool TryParseObject(JsonElement body, out JsonObject request)
{
    request = new JsonObject();
    if (body.ValueKind != JsonValueKind.Object) return false;
    request = JsonNode.Parse(body.GetRawText())?.AsObject() ?? new JsonObject();
    return true;
}

static string? ReadString(JsonElement body, string name)
{
    if (body.ValueKind != JsonValueKind.Object) return null;
    if (!body.TryGetProperty(name, out var element)) return null;
    return element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => element.GetRawText()
    };
}
