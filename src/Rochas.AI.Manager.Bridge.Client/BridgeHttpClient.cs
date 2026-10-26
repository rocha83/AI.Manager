using System.Text.Json;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Client;

/// <summary>
/// Cliente HTTP tipado. Nao conhece SQLite, dominio nem ferramentas: fala apenas
/// o contrato publico do host self-hosted.
/// </summary>
public sealed class BridgeHttpClient : IBridgeClient
{
    private readonly HttpClient httpClient;
    private readonly BridgeClientOptions options;

    public BridgeHttpClient(HttpClient httpClient, BridgeClientOptions? options = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.options = options ?? new BridgeClientOptions();
        if (this.httpClient.Timeout == Timeout.InfiniteTimeSpan || this.httpClient.Timeout > this.options.Timeout)
            this.httpClient.Timeout = this.options.Timeout;
    }

    public async Task<BridgeStatusResponse> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        using var request = BridgeClientHttp.CreateRequest(
            HttpMethod.Get,
            BridgeClientHttp.Combine(options, "/api/status"),
            options);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await BridgeClientHttp.EnsureSuccessAsync(response, cancellationToken);
        return await BridgeClientHttp.ReadJsonAsync<BridgeStatusResponse>(response, cancellationToken);
    }

    public async Task<BridgeComponentInfo> GetContractAsync(CancellationToken cancellationToken = default)
    {
        using var request = BridgeClientHttp.CreateRequest(
            HttpMethod.Get,
            BridgeClientHttp.Combine(options, "/api/contract"),
            options);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await BridgeClientHttp.EnsureSuccessAsync(response, cancellationToken);
        var contract = await BridgeClientHttp.ReadJsonAsync<BridgeComponentInfo>(response, cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.RequiredContractVersion)
            && !string.Equals(options.RequiredContractVersion, contract.ContractVersion, StringComparison.Ordinal))
        {
            throw new BridgeClientException(
                $"contrato remoto {contract.ContractVersion} difere do exigido {options.RequiredContractVersion}",
                code: "client.contract.mismatch");
        }

        if (!BridgeContractVersion.IsContractCompatible(contract.ContractVersion))
        {
            throw new BridgeClientException(
                $"contrato remoto {contract.ContractVersion} incompativel com {BridgeContractVersion.ContractVersion}",
                code: "client.contract.incompatible");
        }

        return contract;
    }

    public async Task<JsonDocument> GetManifestSchemaAsync(CancellationToken cancellationToken = default)
    {
        using var request = BridgeClientHttp.CreateRequest(
            HttpMethod.Get,
            BridgeClientHttp.Combine(options, "/api/contract/schema"),
            options);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await BridgeClientHttp.EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }

    public async Task<BridgeValidationResult> ValidateManifestAsync(BridgeManifest manifest, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToNode(manifest, BridgeClientOptions.JsonOptions);
        return await PostJsonAsync<BridgeValidationResult>("/api/manifest/validate", body, cancellationToken);
    }

    public async Task<BridgeManifestDryRun> DryRunManifestAsync(BridgeManifest manifest, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToNode(manifest, BridgeClientOptions.JsonOptions);
        return await PostJsonAsync<BridgeManifestDryRun>("/api/manifest/dry-run", body, cancellationToken);
    }

    public async Task<JsonDocument> ChatCompletionsAsync(JsonNode request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = BridgeClientHttp.CreateRequest(
            HttpMethod.Post,
            BridgeClientHttp.Combine(options, "/v1/chat/completions"),
            options,
            request);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await BridgeClientHttp.EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }

    public async Task<JsonDocument> ResponsesAsync(JsonNode request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = BridgeClientHttp.CreateRequest(
            HttpMethod.Post,
            BridgeClientHttp.Combine(options, "/v1/responses"),
            options,
            request);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);
        await BridgeClientHttp.EnsureSuccessAsync(response, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }

    public async Task<BridgeToolResponse> ExecuteToolAsync(BridgeToolRequest request, CancellationToken cancellationToken = default)
    {
        var body = JsonSerializer.SerializeToNode(request, BridgeClientOptions.JsonOptions);
        return await PostJsonAsync<BridgeToolResponse>("/api/tools/execute", body, cancellationToken);
    }

    private async Task<T> PostJsonAsync<T>(string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using var request = BridgeClientHttp.CreateRequest(
            HttpMethod.Post,
            BridgeClientHttp.Combine(options, path),
            options,
            body);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await BridgeClientHttp.EnsureSuccessAsync(response, cancellationToken);
        return await BridgeClientHttp.ReadJsonAsync<T>(response, cancellationToken);
    }
}
