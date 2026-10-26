using System.Diagnostics;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Despachante de tools do host do Bridge: executa handlers leaf no workspace.
/// Sem gates de agente/modo (o consumidor autoriza); o shell mantem as travas
/// internas do <see cref="ShellToolHandler"/> + <see cref="ShellInspector"/>.
/// </summary>
public sealed class BridgeToolExecutor
{
    private readonly Dictionary<string, IToolHandler> _handlers;
    private readonly string _workspaceRoot;
    private readonly string _upstreamUrl;
    private readonly string _apiKey;
    private readonly string _role;
    private readonly Action<JsonObject>? _log;

    /// <summary>Cria o executor para um workspace e role.</summary>
    /// <param name="workspaceRoot">Raiz do jail (cwd do deployment).</param>
    /// <param name="upstreamUrl">Upstream de inferencia para a tool subagent.</param>
    /// <param name="apiKey">Bearer repassado ao upstream.</param>
    /// <param name="role">Role do agente (<c>plan</c>, <c>build</c>, <c>exec</c>).</param>
    /// <param name="log">Sink de log do host.</param>
    public BridgeToolExecutor(
        string workspaceRoot,
        string upstreamUrl = "http://127.0.0.1:4100",
        string apiKey = "",
        string role = "",
        Action<JsonObject>? log = null)
    {
        _workspaceRoot = workspaceRoot;
        _upstreamUrl = upstreamUrl;
        _apiKey = apiKey;
        _role = role;
        _log = log;
        _handlers = DefaultHandlers().ToDictionary(h => h.ToolName, StringComparer.Ordinal);
    }

    /// <summary>Handlers disponiveis por padrao (nomes do protocolo).</summary>
    public static IEnumerable<IToolHandler> DefaultHandlers()
    {
        yield return new ShellToolHandler();
        yield return new ReadToolHandler();
        yield return new WriteToolHandler();
        yield return new EditToolHandler();
        yield return new GrepToolHandler();
        yield return new GlobToolHandler();
        yield return new FetchToolHandler();
        yield return new GitToolHandler();
        yield return new SubagentToolHandler();
    }

    /// <summary>Nomes de tool aceitos pelo executor nesta instancia.</summary>
    public IReadOnlyCollection<string> Tools => _handlers.Keys;

    /// <summary>Executa uma tool pelo nome com argumentos JSON crus.</summary>
    /// <param name="name">Nome anunciado (aliases <c>bash</c>, <c>webfetch</c>, <c>task</c> sao aceitos).</param>
    /// <param name="arguments">Objeto JSON de argumentos (texto).</param>
    /// <param name="timeoutSeconds">Teto de tempo da tool.</param>
    public ToolResult Execute(string name, string arguments, int timeoutSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ToolResult.Fail("tool sem nome");

        string key = name switch
        {
            "bash" or "sh" => "shell",
            "webfetch" => "fetch",
            "task" => "subagent",
            _ => name
        };
        if (!_handlers.TryGetValue(key, out IToolHandler? handler))
            return ToolResult.Fail(
                $"tool desconhecida: '{name}' (validas: {string.Join(", ", _handlers.Keys.OrderBy(k => k, StringComparer.Ordinal))})");

        var context = new ToolContext
        {
            WorkspaceRoot = _workspaceRoot,
            TimeoutSeconds = timeoutSeconds,
            UpstreamUrl = _upstreamUrl,
            ApiKey = _apiKey,
            Role = _role,
            ExtraRoots = ToolContext.DefaultExtraRoots,
            Log = _log
        };

        var sw = Stopwatch.StartNew();
        try
        {
            var result = handler.Handle(arguments ?? "", context);
            ToolMetrics.Executed(key, result.Success);
            _log?.Invoke(new JsonObject
            {
                ["event"] = "tool",
                ["tool"] = key,
                ["ok"] = result.Success,
                ["ms"] = sw.ElapsedMilliseconds
            });
            return result;
        }
        catch (Exception ex)
        {
            ToolMetrics.Executed(key, false);
            return ToolResult.Fail($"falha interna na tool '{name}': {ex.Message}");
        }
    }

    /// <summary>
    /// Executa o contrato HTTP do host (<see cref="BridgeToolRequest"/>) e
    /// devolve o contrato de resposta com metadados e duracao.
    /// </summary>
    public BridgeToolResponse Execute(BridgeToolRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sw = Stopwatch.StartNew();
        string args = request.Arguments.ToJsonString();
        var result = Execute(request.Name, args, request.TimeoutSec > 0 ? request.TimeoutSec : 300);
        sw.Stop();
        return new BridgeToolResponse
        {
            Success = result.Success,
            Output = result.Success ? result.Output : null,
            Error = result.Success ? null : result.Error,
            DurationMs = sw.ElapsedMilliseconds,
            Metadata = new JsonObject
            {
                ["tool"] = request.Name,
                ["workspace"] = string.IsNullOrWhiteSpace(request.WorkspaceRoot)
                    ? _workspaceRoot
                    : request.WorkspaceRoot,
                ["role"] = string.IsNullOrWhiteSpace(request.Role) ? _role : request.Role
            }
        };
    }

    /// <summary>Converte o resultado para o JSON curto usado pelo loop do modelo.</summary>
    public static JsonObject ToModelJson(ToolResult result) => new()
    {
        ["ok"] = result.Success,
        ["output"] = result.Success ? result.Output : "",
        ["error"] = result.Success ? "" : result.Error
    };
}
