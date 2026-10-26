using System.Text.Json;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge;
using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Bridge.Flow;
using Rochas.AI.Manager.Bridge.Hosting;
using Rochas.AI.Manager.Bridge.Transport;

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

app.MapPost("/v1/responses", async (
    JsonElement body,
    HttpContext context,
    BridgeProxy proxy,
    BridgeOptions bridgeOptions,
    CancellationToken cancellationToken) =>
{
    if (!TryParseObject(body, out var request))
        return Results.BadRequest(new { error = "json object required" });

    var isStream = request["stream"] is JsonValue streamValue
        && streamValue.TryGetValue<bool>(out var streamRequested)
        && streamRequested;

    if (!isStream)
    {
        try
        {
            var result = await proxy.CompleteAsync(request, bridgeOptions, cancellationToken);
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
        await foreach (var item in proxy.StreamAsync(request, bridgeOptions, cancellationToken))
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

    var isStream = request["stream"] is JsonValue streamValue
        && streamValue.TryGetValue<bool>(out var streamRequested)
        && streamRequested;

    if (!isStream)
    {
        try
        {
            var result = await proxy.ForwardChatAsync(request, bridgeOptions, cancellationToken);
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
        await foreach (var line in proxy.ForwardChatStreamAsync(request, bridgeOptions, cancellationToken))
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
