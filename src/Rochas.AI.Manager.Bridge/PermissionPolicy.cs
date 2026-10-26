namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Politica de tools do caminho gerenciado. Delega ao MESMO inspetor/validador
/// que o executor usa (nenhuma regra duplicada): shell via
/// <c>ShellInspector.Validate</c> e git via <c>GitToolHandler.Validate</c>.
/// A allowlist por agente continua no consumidor (Web/runner).
/// </summary>
public static class PermissionPolicy
{
    private static readonly string[] Roles =
    {
        "plan", "build", "orch", "moe", "scripter", "exec"
    };

    /// <summary>Decide a acao (allow/deny) para uma chamada de tool.</summary>
    /// <param name="model">Modelo/role do agente (pode carregar sufixo de role).</param>
    /// <param name="tool">Nome da tool.</param>
    /// <param name="args">Argumentos serializados.</param>
    /// <returns>Acao e motivo; risco de shell/git e fail-closed.</returns>
    public static (string Action, string Reason) Check(string? model, string tool, string args)
    {
        if (string.IsNullOrWhiteSpace(tool)) return ("deny", "tool sem nome");

        string role = RoleOf(model);
        string command = Tools.ShellInspector.ExtractCommand(args);

        if (tool is "shell" or "bash")
        {
            string? why = Tools.ShellInspector.Validate(command);
            return why is null ? ("allow", "") : ("deny", why);
        }

        if (tool is "git")
        {
            string? why = Tools.GitToolHandler.Validate(command, role, out _);
            return why is null ? ("allow", "") : ("deny", why);
        }

        return ("allow", "");
    }

    /// <summary>Role a partir do sufixo do modelo (<c>...-plan</c>); vazio se ausente.</summary>
    public static string RoleOf(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return "";
        string m = model.Trim();
        int dash = m.LastIndexOf('-');
        if (dash <= 0 || dash == m.Length - 1) return "";
        string suffix = m[(dash + 1)..];
        return Roles.Contains(suffix, StringComparer.OrdinalIgnoreCase)
            ? suffix.ToLowerInvariant()
            : "";
    }
}
