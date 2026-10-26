using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Client;

/// <summary>
/// Leitor SSE do host. Devolve eventos ja parseados (event + data) e mantem o
/// data multi-linha; comentarios (<c>:</c>) e keep-alives sao ignorados.
/// </summary>
public sealed class BridgeSseClient
{
    private readonly HttpClient httpClient;
    private readonly BridgeClientOptions options;

    public BridgeSseClient(HttpClient httpClient, BridgeClientOptions? options = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? new BridgeClientOptions();
    }

    public IAsyncEnumerable<BridgeSseEvent> StreamResponsesAsync(JsonNode request, CancellationToken cancellationToken = default) =>
        StreamAsync("/v1/responses", request, cancellationToken);

    public IAsyncEnumerable<BridgeSseEvent> StreamChatCompletionsAsync(JsonNode request, CancellationToken cancellationToken = default) =>
        StreamAsync("/v1/chat/completions", request, cancellationToken);

    public async IAsyncEnumerable<BridgeSseEvent> StreamAsync(
        string path,
        JsonNode request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var httpRequest = BridgeClientHttp.CreateRequest(
            HttpMethod.Post,
            BridgeClientHttp.Combine(options, path),
            options,
            request);

        using var response = await httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        await BridgeClientHttp.EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);

        string? eventName = null;
        var data = new StringBuilder();
        var hasData = false;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;

            if (line.Length == 0)
            {
                if (hasData)
                {
                    yield return new BridgeSseEvent(eventName, data.ToString());
                    eventName = null;
                    data.Clear();
                    hasData = false;
                }
                continue;
            }

            if (line[0] == ':') continue;

            var separator = line.IndexOf(':');
            var field = separator < 0 ? line : line[..separator];
            var value = separator < 0 ? string.Empty : line[(separator + 1)..];
            if (value.StartsWith(' ')) value = value[1..];

            switch (field)
            {
                case "event":
                    eventName = value;
                    break;
                case "data":
                    if (hasData) data.Append('\n');
                    data.Append(value);
                    hasData = true;
                    break;
                default:
                    // id/retry/outros campos nao fazem parte do contrato do Bridge.
                    break;
            }
        }

        if (hasData)
            yield return new BridgeSseEvent(eventName, data.ToString());
    }
}
