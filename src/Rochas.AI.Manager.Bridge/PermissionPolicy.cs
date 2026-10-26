namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Politica de tools do caminho gerenciado. A allowlist por agente e aplicada
/// pelo consumidor (Web/runner); aqui o padrao e liberar. O host mantem as
/// regras de risco/role em <c>/api/permission/check</c>.
/// </summary>
public static class PermissionPolicy
{
    /// <summary>Decide a acao (allow/ask/deny) para uma chamada de tool.</summary>
    /// <param name="model">Modelo/role do agente (pode carregar sufixo de role).</param>
    /// <param name="tool">Nome da tool.</param>
    /// <param name="args">Argumentos serializados.</param>
    /// <returns>Acao e motivo; o default e <c>allow</c>.</returns>
    public static (string Action, string Reason) Check(string? model, string tool, string args)
    {
        // O shell ja tem o inspetor fail-closed dentro do handler. A politica
        // externa nao duplica a regra para nao divergir do executor real.
        if (string.IsNullOrWhiteSpace(tool)) return ("deny", "tool sem nome");
        return ("allow", "");
    }
}
