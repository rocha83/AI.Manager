using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Handler da tool <c>fetch</c>: GET HTTP somente-leitura (tipo curl).
/// Sem escrita e sem execucao; devolve o corpo como texto truncado.
/// </summary>
public sealed class FetchToolHandler : IToolHandler
{
    /// <inheritdoc />
    public string ToolName => "fetch";

    private const int DefaultMaxChars = 12000;
    private const int MaxChars = 1_000_000;

    private static readonly HttpClient Http = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
    });

    /// <inheritdoc />
    public ToolResult Handle(string argumentsJson, ToolContext context)
    {
        JsonObject? obj;
        try { obj = JsonNode.Parse(argumentsJson)?.AsObject(); }
        catch (Exception ex) { return ToolResult.Fail($"argumentos invalidos: {ex.Message}"); }
        if (obj is null) return ToolResult.Fail("argumentos invalidos: objeto JSON esperado");

        string url = obj["url"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(url)) return ToolResult.Fail("url e obrigatoria");
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
            return ToolResult.Fail("url deve ser http(s) absoluta");
        int maxChars = Math.Min(Math.Max(obj["maxChars"]?.GetValue<int>() ?? DefaultMaxChars, 1), MaxChars);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(30, context.TimeoutSeconds)));
            using var res = Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode)
                return ToolResult.Fail($"HTTP {(int)res.StatusCode} em '{uri}'");
            string body = res.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult() ?? "";
            if (body.Length > maxChars) body = body[..maxChars];
            return ToolResult.Ok(body);
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("fetch estourou o tempo");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"falha ao buscar '{url}': {ex.Message.Split('\n')[0]}");
        }
    }
}
