namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Resultado de uma execucao de tool (objeto de valor do dominio de tools).
/// </summary>
public sealed class ToolResult
{
    /// <summary>Verdadeiro apenas quando a tool concluiu o efeito pedido.</summary>
    public bool Success { get; init; }

    /// <summary>Saida textual da tool (vazia em falha).</summary>
    public string Output { get; init; } = "";

    /// <summary>Motivo da falha (vazio em sucesso).</summary>
    public string Error { get; init; } = "";

    /// <summary>Cria um resultado bem-sucedido.</summary>
    public static ToolResult Ok(string output) => new() { Success = true, Output = output };

    /// <summary>Cria um resultado de falha honesta.</summary>
    public static ToolResult Fail(string error) => new() { Success = false, Error = error };
}
