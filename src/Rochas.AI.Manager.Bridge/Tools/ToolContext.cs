using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Contexto unico de execucao das tools no host do Bridge. O deployment decide
/// as raizes extras; o modelo so ve o workspace e os caminhos absolutos que o
/// jail permitir.
/// </summary>
public sealed class ToolContext
{
    /// <summary>Raizes extras permitidas fora do workspace (politica do host).</summary>
    public static readonly string[] DefaultExtraRoots = { "/mnt", "/media", "/tmp" };

    /// <summary>Raizes alem do workspace que o host aceita (ex.: cenarios em /tmp).</summary>
    public string[] ExtraRoots { get; init; } = DefaultExtraRoots;

    /// <summary>Raiz do jail; todo caminho relativo resolve contra ela.</summary>
    public string WorkspaceRoot { get; init; } = "";

    /// <summary>Teto de tempo de cada processo/tool, em segundos.</summary>
    public int TimeoutSeconds { get; init; } = 300;

    /// <summary>Upstream de inferencia usado pela tool de subagente.</summary>
    public string UpstreamUrl { get; init; } = "http://127.0.0.1:4100";

    /// <summary>Bearer opcional repassado ao upstream pela tool de subagente.</summary>
    public string ApiKey { get; init; } = "";

    /// <summary>
    /// Role do agente dono da chamada (<c>plan</c>, <c>build</c>, <c>exec</c>...).
    /// <c>plan</c> e somente leitura; <c>build</c>/<c>exec</c> escrevem.
    /// </summary>
    public string Role { get; init; } = "";

    /// <summary>Sink de log do host (uma linha JSON por evento relevante).</summary>
    public Action<JsonObject>? Log { get; init; }
}
