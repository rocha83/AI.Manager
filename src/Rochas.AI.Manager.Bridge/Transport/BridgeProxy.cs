using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Transport;

/// <summary>Configuracao do transporte upstream (llama.cpp) do bridge.</summary>
public sealed class BridgeProxyOptions
{
    public string UpstreamUrl { get; set; } = "http://127.0.0.1:4100";
    public string ModelId { get; set; } = "Gemma4-E4B-Q4";
    public string? ApiKey { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>Evento emitido no stream Responses (tipo + payload do SSE).</summary>
public sealed record BridgeProxyEvent(string Type, JsonObject Payload);

/// <summary>Resposta nao-stream ja convertida, com o corpo bruto do upstream.</summary>
public sealed record BridgeProxyResult(JsonObject Body, string Raw, int StatusCode);

/// <summary>Falha do upstream preservando status e corpo para o host decidir.</summary>
public sealed class BridgeProxyException : Exception
{
    public BridgeProxyException(string message, int statusCode, string responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public int StatusCode { get; }
    public string ResponseBody { get; }
}

/// <summary>
/// Transporte de protocolo do bridge: traduz Responses -> chat, chama o upstream,
/// converte de volta e sintetiza o SSE. Nao conhece HTTP server, CLI, Swagger nem
/// banco: quem hospeda e o Api ou o Runner.
/// </summary>
public sealed class BridgeProxy
{
    private readonly HttpClient httpClient;
    private readonly BridgeProxyOptions options;

    public BridgeProxy(HttpClient httpClient, BridgeProxyOptions options)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public BridgeProxyOptions Options => options;

    /// <summary>Responses nao-stream: traduz, chama chat/completions e converte de volta.</summary>
    public async Task<BridgeProxyResult> CompleteAsync(
        JsonObject request,
        BridgeOptions bridgeOptions,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bridgeOptions);

        var converted = ResponsesToChat.Convert(request, bridgeOptions, options.ModelId);
        if (converted.BreakerTrip is not null)
        {
            var trip = ChatToResponses.TripResponse(
                converted.BreakerTrip, options.ModelId, converted.Temperature);
            return new BridgeProxyResult(trip, trip.ToJsonString(JsonOps.SerializerOptions), 200);
        }

        converted.Body["stream"] = false;
        var shimmed = ApplyShim(converted.Body, bridgeOptions);

        using var response = await PostAsync("chat/completions", converted.Body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new BridgeProxyException(
                $"upstream respondeu {(int)response.StatusCode}",
                (int)response.StatusCode,
                raw);

        ThrowIfContextTripped(raw, bridgeOptions);

        var chat = JsonNode.Parse(raw)?.AsObject()
            ?? throw new BridgeProxyException("upstream devolveu JSON invalido", 502, raw);

        if (shimmed) ToolShim.TransformResponse(chat);

        var body = ChatToResponses.Convert(
            chat,
            options.ModelId,
            converted.Temperature,
            bridgeOptions,
            thinkMode: converted.Thinking,
            hadTools: converted.HadTools,
            announcedTools: converted.AnnouncedTools,
            historyMessages: converted.HistoryMessages);

        return new BridgeProxyResult(body, raw, (int)response.StatusCode);
    }

    /// <summary>Responses em stream: consome o SSE do chat e emite os eventos Responses.</summary>
    public async IAsyncEnumerable<BridgeProxyEvent> StreamAsync(
        JsonObject request,
        BridgeOptions bridgeOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bridgeOptions);

        // Shim: upstream non-stream + envelope JSON; o SSE e sintetizado a partir
        // do objeto Responses final (mesmo caminho do host legado).
        if (ShouldShim(request, bridgeOptions))
        {
            var shimResult = await CompleteAsync(request, bridgeOptions, cancellationToken);
            foreach (var shimEvent in ParseSse(ResponsesSse.Build(shimResult.Body, bridgeOptions.Thinking)))
                yield return shimEvent;
            yield break;
        }

        var channel = Channel.CreateUnbounded<BridgeProxyEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });

        _ = Task.Run(async () =>
        {
            try
            {
                var converted = ResponsesToChat.Convert(request, bridgeOptions, options.ModelId);
                if (converted.BreakerTrip is not null)
                {
                    var trip = ChatToResponses.TripResponse(
                        converted.BreakerTrip, options.ModelId, converted.Temperature);
                    foreach (var item in ParseSse(ResponsesSse.Build(trip, bridgeOptions.Thinking)))
                        channel.Writer.TryWrite(item);
                    channel.Writer.TryComplete();
                    return;
                }

                converted.Body["stream"] = true;

                using var response = await PostAsync("chat/completions", converted.Body, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(cancellationToken);
                    throw new BridgeProxyException(
                        $"upstream respondeu {(int)response.StatusCode}",
                        (int)response.StatusCode,
                        error);
                }

                var translator = new ResponsesStreamTranslator(
                    bridgeOptions,
                    (type, payload) => channel.Writer.TryWrite(new BridgeProxyEvent(type, payload)),
                    options.ModelId,
                    converted.Temperature,
                    converted.HadTools,
                    converted.AnnouncedTools,
                    converted.HistoryMessages);

                translator.Start();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                var finished = false;
                while (!cancellationToken.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(cancellationToken);
                    if (line is null) break;
                    if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

                    var data = line[5..].Trim();
                    if (data.Length == 0) continue;
                    if (data == "[DONE]")
                    {
                        translator.Finish();
                        finished = true;
                        break;
                    }

                    var chunk = JsonNode.Parse(data)?.AsObject();
                    if (chunk is not null) translator.Feed(chunk);

                    if (translator.RepeatTripped)
                    {
                        // Contencao: fecha o turno no ponto repetido e encerra o upstream.
                        translator.Finish();
                        finished = true;
                        break;
                    }
                }

                if (!finished && !cancellationToken.IsCancellationRequested)
                    translator.Finish();

                channel.Writer.TryComplete();
            }
            catch (Exception exception)
            {
                channel.Writer.TryComplete(exception);
            }
        }, cancellationToken);

        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
            yield return item;
    }

    /// <summary>Repasse cru de chat/completions (o cliente ja fala o protocolo chat).</summary>
    public async Task<BridgeProxyResult> ForwardChatAsync(
        JsonObject request,
        BridgeOptions bridgeOptions,
        CancellationToken cancellationToken = default)
    {
        var body = (JsonObject)request.DeepClone();
        PrepareChat(body, bridgeOptions);
        var shimmed = ApplyShim(body, bridgeOptions);

        using var response = await PostAsync("chat/completions", body, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new BridgeProxyException(
                $"upstream respondeu {(int)response.StatusCode}",
                (int)response.StatusCode,
                raw);

        ThrowIfContextTripped(raw, bridgeOptions);

        var result = JsonNode.Parse(raw)?.AsObject()
            ?? throw new BridgeProxyException("upstream devolveu JSON invalido", 502, raw);
        if (shimmed) ToolShim.TransformResponse(result);
        if (bridgeOptions.QwenStandard) QwenStandard.TranslateCompletionForClient(result);
        return new BridgeProxyResult(result, raw, (int)response.StatusCode);
    }

    /// <summary>Repasse cru do SSE de chat/completions.</summary>
    public async IAsyncEnumerable<string> ForwardChatStreamAsync(
        JsonObject request,
        BridgeOptions bridgeOptions,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (ShouldShim(request, bridgeOptions))
        {
            var result = await ForwardChatAsync(request, bridgeOptions, cancellationToken);
            var choice = result.Body["choices"]?[0]?.AsObject();
            var message = choice?["message"]?.AsObject() ?? new JsonObject();
            var chunk = new JsonObject
            {
                ["id"] = result.Body["id"]?.GetValue<string>() ?? ("chatcmpl_" + JsonOps.NewId()),
                ["object"] = "chat.completion.chunk",
                ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["model"] = options.ModelId,
                ["choices"] = new JsonArray(new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = message.DeepClone(),
                    ["finish_reason"] = choice?["finish_reason"]?.DeepClone()
                })
            };
            yield return "data: " + chunk.ToJsonString(JsonOps.SerializerOptions);
            yield return string.Empty;
            yield return "data: [DONE]";
            yield return string.Empty;
            yield break;
        }

        var body = (JsonObject)request.DeepClone();
        PrepareChat(body, bridgeOptions);

        using var response = await PostAsync("chat/completions", body, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new BridgeProxyException(
                $"upstream respondeu {(int)response.StatusCode}",
                (int)response.StatusCode,
                error);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            yield return bridgeOptions.QwenStandard ? QwenStandard.TranslateChunkNames(line) : line;
        }
    }

    /// <summary>Higiene do catalogo chat antes do upstream (Code Mode/patterns).</summary>
    private static void PrepareChat(JsonObject body, BridgeOptions bridgeOptions)
    {
        if (body["tools"] is not JsonArray tools) return;
        if (bridgeOptions.ToolStrategy)
        {
            ToolSanitizer.StripExecuteTools(tools, true, "chat");
            ToolSanitizer.NeutralizeCodeMode(body, true);
        }
        ToolSanitizer.StripUnanchoredPatterns(tools, "$");
        if (bridgeOptions.QwenStandard) QwenStandard.TranslateToolsForUpstream(tools);
    }

    private static void ThrowIfContextTripped(string raw, BridgeOptions bridgeOptions)
    {
        var settings = new ContextGuardSettings(
            bridgeOptions.ContextGuard,
            bridgeOptions.ContextThreshold,
            bridgeOptions.ContextTokens);
        if (!ContextGuard.Tripped(raw, settings, out var promptTokens, out var fraction)) return;

        var payload = ContextGuard.BuildPayload(promptTokens, fraction, bridgeOptions.ContextTokens);
        throw new BridgeProxyException(
            $"contexto em {fraction:P0}; compacte a sessao",
            413,
            payload.ToJsonString(JsonOps.SerializerOptions));
    }

    private static bool ShouldShim(JsonObject body, BridgeOptions bridgeOptions) =>
        bridgeOptions.ToolShimMode.Equals("json", StringComparison.OrdinalIgnoreCase)
        && body["tools"] is JsonArray { Count: > 0 };

    private static bool ApplyShim(JsonObject body, BridgeOptions bridgeOptions)
    {
        if (!ShouldShim(body, bridgeOptions)) return false;
        return ToolShim.Apply(
            body,
            bridgeOptions.ToolShimMaxString,
            bridgeOptions.ToolShimMinCalls,
            bridgeOptions.MaxTokens,
            out _);
    }

    private static IEnumerable<BridgeProxyEvent> ParseSse(string sse)
    {
        foreach (var block in sse.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? type = null;
            string? data = null;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("event:", StringComparison.Ordinal))
                    type = line[6..].Trim();
                else if (line.StartsWith("data:", StringComparison.Ordinal))
                    data = line[5..].Trim();
            }

            if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(data) || data == "[DONE]")
                continue;

            var payload = JsonNode.Parse(data)?.AsObject();
            if (payload is not null)
                yield return new BridgeProxyEvent(type, payload);
        }
    }

    private async Task<HttpResponseMessage> PostAsync(
        string path,
        JsonObject body,
        CancellationToken cancellationToken)
    {
        var baseUrl = (options.UpstreamUrl ?? string.Empty).TrimEnd('/');
        if (!Uri.TryCreate($"{baseUrl}/v1/{path}", UriKind.Absolute, out var uri))
            throw new BridgeProxyException($"upstream invalido: '{options.UpstreamUrl}'", 500, string.Empty);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(
                body.ToJsonString(JsonOps.SerializerOptions),
                Encoding.UTF8,
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }
}
