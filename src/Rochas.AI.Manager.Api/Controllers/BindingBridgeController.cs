using Rochas.Data.Specification.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Rochas.AI.Manager.Application.Helpers;
using Rochas.AI.Manager.Domain.Entities;
using Rochas.AI.Manager.Domain.Harness;
using Rochas.AI.Manager.Domain.Interfaces;

namespace Rochas.AI.Manager.Api.Controllers;

[ApiController]
[Route("v1")]
public sealed class BindingBridgeController : ControllerBase
{
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IModelBindingService modelBindingService;
    private readonly IAgentService agentService;
    private readonly IGenericRepository<HarnessRun> harnessRunRepository;
    private readonly IConfiguration configuration;

    public BindingBridgeController(
        IHttpClientFactory httpClientFactory,
        IModelBindingService modelBindingService,
        IAgentService agentService,
        IGenericRepository<HarnessRun> harnessRunRepository,
        IConfiguration configuration)
    {
        this.httpClientFactory = httpClientFactory;
        this.modelBindingService = modelBindingService;
        this.agentService = agentService;
        this.harnessRunRepository = harnessRunRepository;
        this.configuration = configuration;
    }

    [HttpPost("chat/completions")]
    public Task<IActionResult> ChatCompletions([FromBody] JsonElement requestBody, CancellationToken cancellationToken)
        => ForwardAsync("chat/completions", requestBody, sanitizeChat: true, cancellationToken);

    [HttpPost("responses")]
    public Task<IActionResult> Responses([FromBody] JsonElement requestBody, CancellationToken cancellationToken)
        => ForwardAsync("responses", requestBody, sanitizeChat: false, cancellationToken);

    /// <summary>
    /// Unico caminho de encaminhamento das duas rotas: resolve o endpoint da
    /// bridge (agente > binding > default), injeta o perfil, repassa o stream
    /// linha a linha e registra o HarnessRun. As rotas so escolhem o path e se
    /// a resposta leva a limpeza de chat (reasoning removido + coercao de tool).
    /// </summary>
    private async Task<IActionResult> ForwardAsync(
        string path, JsonElement requestBody, bool sanitizeChat, CancellationToken cancellationToken)
    {
        var authorizationHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { error = "missing bearer token" });
        }

        var bindings = await modelBindingService.ListAsync(cancellationToken);
        var binding = bindings.FirstOrDefault();
        if (binding is null)
        {
            return Problem("No model binding configured.");
        }

        var agent = await ResolveAgentAsync(requestBody, cancellationToken);

        // Chat: guarda de contexto (0.95) + temperatura do agente no corpo.
        // Responses: passthrough (a bridge ja e quem traduz o protocolo).
        JsonElement effectiveBody = requestBody;
        if (sanitizeChat)
        {
            int estimatedTokens = requestBody.GetRawText().Length / 4;
            int contextThreshold = (int)(binding.ContextWindow * 0.95);
            if (estimatedTokens > contextThreshold)
            {
                return StatusCode(413, new { error = $"context exceeded: {estimatedTokens} > {contextThreshold}" });
            }
            effectiveBody = WithAgentTemperature(requestBody, agent);
        }

        var upstreamUrl = ResolveUpstreamBaseUrl(agent, binding) + "/v1/" + path;

        bool isStream = effectiveBody.TryGetProperty("stream", out var streamProperty)
            && streamProperty.ValueKind == JsonValueKind.True;

        var httpClient = httpClientFactory.CreateClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, upstreamUrl)
        {
            Content = new StringContent(effectiveBody.GetRawText(), System.Text.Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", configuration["Bridge:ApiKey"] ?? "guest@123");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var upstreamResponse = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (isStream)
        {
            Response.StatusCode = (int)upstreamResponse.StatusCode;
            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            await using var upstreamStream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(upstreamStream);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) is not null)
            {
                await Response.WriteAsync(line + "\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            return new EmptyResult();
        }

        var responseJson = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
        if (sanitizeChat)
        {
            responseJson = ReasoningSanitizer.Sanitize(responseJson);
            try
            {
                var root = JsonNode.Parse(responseJson)?.AsObject();
                if (root is not null && ToolCallCoercer.CoerceChatCalls(root) > 0)
                {
                    responseJson = root.ToJsonString();
                }
            }
            catch { }
        }
        else
        {
            // Reasoning pode vir com tags de controle do template; limpa sem
            // mexer no shape (content[].text do item type=reasoning).
            responseJson = ReasoningSanitizer.SanitizeResponses(responseJson);
        }

        stopwatch.Stop();
        try
        {
            var run = new HarnessRun
            {
                Name = sanitizeChat ? "proxy" : "proxy-responses",
                EvidenceId = 0,
                ProfileVersionId = 0,
                ModelBindingId = binding.Id,
                ResultCode = (int)upstreamResponse.StatusCode,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                TokensPerSecond = responseJson.Length / 4.0 / Math.Max(0.001, stopwatch.Elapsed.TotalSeconds)
            };
            await harnessRunRepository.Add(run);
        }
        catch { }

        Response.StatusCode = (int)upstreamResponse.StatusCode;
        return Content(responseJson, "application/json");
    }

    /// <summary>Agente do role: body.model pode ser o nome do role ou o model id.</summary>
    private async Task<Agent?> ResolveAgentAsync(JsonElement requestBody, CancellationToken cancellationToken)
    {
        if (!requestBody.TryGetProperty("model", out var modelProperty)
            || modelProperty.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var modelName = modelProperty.GetString();
        if (string.IsNullOrWhiteSpace(modelName)) return null;

        var agents = await agentService.ListAsync(cancellationToken);
        return agents.FirstOrDefault(currentAgent =>
            currentAgent.Name.Equals(modelName, StringComparison.OrdinalIgnoreCase) ||
            currentAgent.Model.Equals(modelName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Endpoint da bridge: o agente manda (Agent.BridgeUrl) porque cada role tem
    /// sua bridge (GPU/CPU); sem agente, cai no provider do binding e por fim no
    /// default do appsettings (Bridge:GpuUrl / Bridge:CpuUrl).
    /// </summary>
    private string ResolveUpstreamBaseUrl(Agent? agent, ModelBinding binding)
    {
        if (agent is not null && !string.IsNullOrWhiteSpace(agent.BridgeUrl))
        {
            return agent.BridgeUrl.TrimEnd('/');
        }
        var isCpu = binding.Provider.Equals("llama", StringComparison.OrdinalIgnoreCase);
        var configured = configuration[isCpu ? "Bridge:CpuUrl" : "Bridge:GpuUrl"];
        return (string.IsNullOrWhiteSpace(configured)
            ? (isCpu ? "http://localhost:4125" : "http://localhost:4124")
            : configured).TrimEnd('/');
    }

    /// <summary>Copia o corpo com a temperatura do agente (se houver agente).</summary>
    private static JsonElement WithAgentTemperature(JsonElement requestBody, Agent? agent)
    {
        if (agent is null) return requestBody;
        try
        {
            var node = JsonNode.Parse(requestBody.GetRawText())?.AsObject();
            if (node is null) return requestBody;
            node["temperature"] = agent.Temperature;
            return JsonDocument.Parse(node.ToJsonString()).RootElement;
        }
        catch { return requestBody; }
    }
}
