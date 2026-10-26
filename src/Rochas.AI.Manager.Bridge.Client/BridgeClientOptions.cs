using System.Text.Json;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Client;

/// <summary>Configuracao do cliente: endereco do host, autenticacao e timeout.</summary>
public sealed class BridgeClientOptions
{
    public string BaseUrl { get; set; } = "http://127.0.0.1:4140";
    public string? ApiKey { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(120);
    public string UserAgent { get; set; } = "Rochas.AI.Manager.Bridge.Client/1.0";

    /// <summary>Versao de contrato exigida; null aceita qualquer major compativel.</summary>
    public string? RequiredContractVersion { get; set; }

    public static JsonSerializerOptions JsonOptions { get; } = new(ManifestCanonicalizer.SerializerOptions)
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>Porta de transporte do host usada pelo AI.Manager e por consumidores externos.</summary>
public interface IBridgeClient
{
    Task<BridgeStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default);
    Task<BridgeComponentInfo> GetContractAsync(CancellationToken cancellationToken = default);
    Task<JsonDocument> GetManifestSchemaAsync(CancellationToken cancellationToken = default);
    Task<BridgeValidationResult> ValidateManifestAsync(BridgeManifest manifest, CancellationToken cancellationToken = default);
    Task<BridgeManifestDryRun> DryRunManifestAsync(BridgeManifest manifest, CancellationToken cancellationToken = default);
    Task<JsonDocument> ChatCompletionsAsync(JsonNode request, CancellationToken cancellationToken = default);
    Task<JsonDocument> ResponsesAsync(JsonNode request, CancellationToken cancellationToken = default);
    Task<BridgeToolResponse> ExecuteToolAsync(BridgeToolRequest request, CancellationToken cancellationToken = default);
}

internal static class BridgeClientHttp
{
    public static Uri Combine(BridgeClientOptions options, string path)
    {
        var baseUrl = (options.BaseUrl ?? string.Empty).TrimEnd('/');
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            throw new BridgeClientException($"BaseUrl invalida: '{options.BaseUrl}'", code: "client.baseUrl.invalid");
        return new Uri(baseUri, path);
    }

    public static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, BridgeClientOptions options, JsonNode? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.UserAgent.ParseAdd(options.UserAgent);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
        if (body is not null)
            request.Content = new StringContent(body.ToJsonString(BridgeClientOptions.JsonOptions), System.Text.Encoding.UTF8, "application/json");
        return request;
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        var body = string.Empty;
        try { body = await response.Content.ReadAsStringAsync(cancellationToken); } catch { }
        var code = $"http.{(int)response.StatusCode}";
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
                code = error.GetString() ?? code;
        }
        catch { }

        throw new BridgeClientException(
            $"Bridge respondeu {(int)response.StatusCode} ({response.ReasonPhrase})",
            (int)response.StatusCode,
            body,
            code);
    }

    public static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var value = JsonSerializer.Deserialize<T>(json, BridgeClientOptions.JsonOptions);
        if (value is null)
            throw new BridgeClientException("resposta JSON vazia ou invalida", (int)response.StatusCode, json, "client.json.invalid");
        return value;
    }
}
