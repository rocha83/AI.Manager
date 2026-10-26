namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Contrato de um handler de tool (uma implementacao por tool anunciada).
/// O host do Bridge executa a tool e devolve o resultado ao cliente; o modelo
/// nunca executa nada por conta propria.
/// </summary>
public interface IToolHandler
{
    /// <summary>Nome da tool no protocolo (ex.: <c>shell</c>, <c>read</c>).</summary>
    string ToolName { get; }

    /// <summary>
    /// Executa a tool com os argumentos JSON crus do modelo.
    /// </summary>
    /// <param name="argumentsJson">Objeto JSON de argumentos (pode vir vazio).</param>
    /// <param name="context">Contexto de execucao (workspace, role, limites).</param>
    /// <returns>Resultado tipado; falha sempre carrega o motivo.</returns>
    ToolResult Handle(string argumentsJson, ToolContext context);
}
