using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge;
using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Bridge.Flow;
using Rochas.AI.Manager.Bridge.Hosting;
using Rochas.AI.Manager.Bridge.Transport;

namespace Rochas.AI.Manager.Bridge.Runner;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(ManifestCanonicalizer.SerializerOptions)
    {
        PropertyNameCaseInsensitive = true
    };

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            Console.WriteLine(BridgeHostOptions.Usage);
            Console.WriteLine();
            Console.WriteLine("  manifest --manifest <arquivo.json> [--result <saida.json>] [--workspace <dir>]");
            Console.WriteLine("           valida e resolve o manifesto (dry-run), sem executar nada");
            Console.WriteLine("  serve    [opcoes do host]   sobe o console self-hosted (default)");
            return 0;
        }

        var command = args[0];
        var rest = args[1..];

        return command switch
        {
            "manifest" => RunManifest(rest),
            "serve" => RunServe(rest),
            _ => RunServe(args)
        };
    }

    private static int RunManifest(string[] args)
    {
        var manifestPath = Flag(args, "--manifest", null);
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            Console.Error.WriteLine("manifest: --manifest <arquivo.json> obrigatorio");
            return 2;
        }

        var resultPath = Flag(args, "--result", null);
        var workspace = Flag(args, "--workspace", Directory.GetCurrentDirectory());

        BridgeManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<BridgeManifest>(File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"manifest: JSON invalido: {exception.Message}");
            return 3;
        }

        if (manifest is null)
        {
            Console.Error.WriteLine("manifest: arquivo vazio");
            return 3;
        }

        var validation = BridgeManifestValidator.Validate(manifest);
        if (!validation.IsValid)
        {
            var invalid = JsonSerializer.Serialize(validation, JsonOptions);
            Console.WriteLine(invalid);
            if (!string.IsNullOrWhiteSpace(resultPath)) File.WriteAllText(resultPath, invalid);
            return 4;
        }

        var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions
        {
            WorkspaceRoot = workspace,
            Model = Flag(args, "--model", null),
            RunId = Flag(args, "--run-id", null)
        });

        var output = JsonSerializer.Serialize(dryRun, JsonOptions);
        Console.WriteLine(output);
        if (!string.IsNullOrWhiteSpace(resultPath)) File.WriteAllText(resultPath, output);
        return dryRun.ManifestHashMatches ? 0 : 5;
    }

    private static int RunServe(string[] args)
    {
        var options = BridgeHostOptions.Parse(args);
        if (options.ShowHelp)
        {
            Console.WriteLine(BridgeHostOptions.Usage);
            return 0;
        }

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

        var proxy = new BridgeProxy(
            new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
            new BridgeProxyOptions
            {
                UpstreamUrl = options.UpstreamUrl,
                ModelId = options.ModelId,
                ApiKey = options.ApiKey
            });

        var prefix = options.Listen is "127.0.0.1" or "localhost"
            ? $"http://127.0.0.1:{options.Port}/"
            : $"http://+:{options.Port}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"runner: nao conseguiu escutar em {prefix}: {exception.Message}");
            return 6;
        }

        var startedAt = DateTimeOffset.UtcNow;
        Console.WriteLine(
            $"{BridgeContractVersion.ComponentName} {BridgeComponent.Version} " +
            $"(contract {BridgeContractVersion.ContractVersion}, schema {BridgeContractVersion.SchemaVersion})");
        Console.WriteLine(
            $"runner {prefix} | upstream {options.UpstreamUrl} | " +
            $"role {(options.IsGate ? "gate" : "mirror")}");
        Console.WriteLine("endpoints: /api/status /health /api/contract /api/contract/schema /api/manifest/validate /api/manifest/dry-run /v1/responses /v1/chat/completions");

        var stopping = false;
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            stopping = true;
            listener.Stop();
        };

        while (!stopping && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleAsync(context, options, bridgeOptions, proxy, startedAt));
        }

        return 0;
    }

    private static async Task HandleAsync(
        HttpListenerContext context,
        BridgeHostOptions options,
        BridgeOptions bridgeOptions,
        BridgeProxy proxy,
        DateTimeOffset startedAt)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            var method = context.Request.HttpMethod;

            if (!IsAuthorized(context, options, path))
            {
                await WriteJsonAsync(context, HttpStatusCode.Unauthorized, new { error = "missing or invalid bearer token" });
                return;
            }

            if (method == "GET" && path is "/api/status" or "/health")
            {
                await WriteJsonAsync(context, HttpStatusCode.OK, BridgeComponent.Status(options, startedAt));
                return;
            }
            if (method == "GET" && path == "/api/contract")
            {
                await WriteJsonAsync(context, HttpStatusCode.OK, BridgeComponent.Info());
                return;
            }
            if (method == "GET" && path == "/api/contract/schema")
            {
                await WriteJsonAsync(context, HttpStatusCode.OK, ManifestSchema.Generate());
                return;
            }
            if (method == "POST" && path == "/api/manifest/validate")
            {
                var manifest = await ReadManifestAsync(context);
                if (manifest is null) return;
                var validation = BridgeManifestValidator.Validate(manifest);
                await WriteJsonAsync(
                    context,
                    validation.IsValid ? HttpStatusCode.OK : HttpStatusCode.UnprocessableEntity,
                    validation);
                return;
            }
            if (method == "POST" && path == "/api/manifest/dry-run")
            {
                var manifest = await ReadManifestAsync(context);
                if (manifest is null) return;
                var validation = BridgeManifestValidator.Validate(manifest);
                if (!validation.IsValid)
                {
                    await WriteJsonAsync(context, HttpStatusCode.UnprocessableEntity, validation);
                    return;
                }
                var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions
                {
                    WorkspaceRoot = options.ArtifactRoot,
                    Model = options.ModelId,
                    ExperimentId = manifest.Experiment.Id,
                    PlanId = manifest.Experiment.Version
                });
                await WriteJsonAsync(context, HttpStatusCode.OK, dryRun);
                return;
            }
            if (method == "POST" && path == "/v1/responses")
            {
                await HandleResponsesAsync(context, proxy, bridgeOptions);
                return;
            }
            if (method == "POST" && path == "/v1/chat/completions")
            {
                await HandleChatAsync(context, proxy, bridgeOptions);
                return;
            }

            await WriteJsonAsync(context, HttpStatusCode.NotFound, new { error = $"rota nao encontrada: {method} {path}" });
        }
        catch (BridgeProxyException exception)
        {
            if (context.Response.OutputStream.CanWrite)
                await WriteJsonAsync(context, (HttpStatusCode)exception.StatusCode, new
                {
                    error = exception.Message,
                    upstream = exception.ResponseBody
                });
        }
        catch (Exception exception)
        {
            if (context.Response.OutputStream.CanWrite)
                await WriteJsonAsync(context, HttpStatusCode.InternalServerError, new { error = exception.Message });
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private static async Task HandleResponsesAsync(
        HttpListenerContext context,
        BridgeProxy proxy,
        BridgeOptions bridgeOptions)
    {
        var request = await ReadJsonAsync(context);
        if (request is null) return;
        var effective = bridgeOptions.ForClient(context.Request.UserAgent);

        var isStream = request["stream"] is JsonValue streamValue
            && streamValue.TryGetValue<bool>(out var streamRequested)
            && streamRequested;

        if (!isStream)
        {
            var result = await proxy.CompleteAsync(request, effective);
            await WriteJsonAsync(context, HttpStatusCode.OK, result.Body);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Response.SendChunked = true;

        await foreach (var item in proxy.StreamAsync(request, effective))
        {
            await WriteLineAsync(context,
                $"event: {item.Type}\ndata: {item.Payload.ToJsonString(JsonOps.SerializerOptions)}\n\n");
        }
        await WriteLineAsync(context, "data: [DONE]\n\n");
    }

    private static async Task HandleChatAsync(
        HttpListenerContext context,
        BridgeProxy proxy,
        BridgeOptions bridgeOptions)
    {
        var request = await ReadJsonAsync(context);
        if (request is null) return;
        var effective = bridgeOptions.ForClient(context.Request.UserAgent);

        var isStream = request["stream"] is JsonValue streamValue
            && streamValue.TryGetValue<bool>(out var streamRequested)
            && streamRequested;

        if (!isStream)
        {
            var result = await proxy.ForwardChatAsync(request, effective);
            await WriteJsonAsync(context, HttpStatusCode.OK, result.Body);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers["Cache-Control"] = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no";
        context.Response.SendChunked = true;

        await foreach (var line in proxy.ForwardChatStreamAsync(request, effective))
            await WriteLineAsync(context, line + "\n");
    }

    private static async Task<BridgeManifest?> ReadManifestAsync(HttpListenerContext context)
    {
        var json = await ReadBodyAsync(context);
        if (string.IsNullOrWhiteSpace(json))
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest, new { error = "manifest required" });
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<BridgeManifest>(json, JsonOptions);
            if (manifest is null)
            {
                await WriteJsonAsync(context, HttpStatusCode.BadRequest, new { error = "manifest invalido" });
                return null;
            }
            return manifest;
        }
        catch (Exception exception)
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest, new { error = exception.Message });
            return null;
        }
    }

    private static async Task<JsonObject?> ReadJsonAsync(HttpListenerContext context)
    {
        var json = await ReadBodyAsync(context);
        if (string.IsNullOrWhiteSpace(json))
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest, new { error = "json object required" });
            return null;
        }

        try
        {
            var node = JsonNode.Parse(json)?.AsObject();
            if (node is null)
            {
                await WriteJsonAsync(context, HttpStatusCode.BadRequest, new { error = "json object required" });
                return null;
            }
            return node;
        }
        catch (Exception exception)
        {
            await WriteJsonAsync(context, HttpStatusCode.BadRequest, new { error = exception.Message });
            return null;
        }
    }

    private static async Task<string> ReadBodyAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static bool IsAuthorized(HttpListenerContext context, BridgeHostOptions options, string path)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey)) return true;
        if (path is "/api/status" or "/health" or "/api/contract" or "/api/contract/schema") return true;

        var authorization = context.Request.Headers["Authorization"] ?? string.Empty;
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && string.Equals(authorization["Bearer ".Length..].Trim(), options.ApiKey, StringComparison.Ordinal);
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, HttpStatusCode status, object payload)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json; charset=utf-8";
        var json = payload is JsonNode node
            ? node.ToJsonString(JsonOptions)
            : JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        await context.Response.OutputStream.FlushAsync();
    }

    private static async Task WriteLineAsync(HttpListenerContext context, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await context.Response.OutputStream.WriteAsync(bytes);
        await context.Response.OutputStream.FlushAsync();
    }

    private static string? Flag(string[] args, string name, string? fallback)
    {
        for (var i = 0; i + 1 < args.Length; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }
}
