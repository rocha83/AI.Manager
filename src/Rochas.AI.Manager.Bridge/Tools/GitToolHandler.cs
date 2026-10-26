using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Handler da tool <c>git</c>: leitura para qualquer role; escrita apenas para
/// roles que executam (<c>build</c>/<c>exec</c>). <c>plan</c> e desconhecido sao
/// somente leitura por design — planejar nao grava no repo.
/// </summary>
public sealed class GitToolHandler : IToolHandler
{
    /// <inheritdoc />
    public string ToolName => "git";

    private static readonly HashSet<string> ReadAllowed = new(StringComparer.Ordinal)
    {
        "status", "log", "diff", "show", "branch", "ls-files", "rev-parse",
    };

    private static readonly HashSet<string> WriteAllowed = new(StringComparer.Ordinal)
    {
        "init", "add", "commit", "rm", "mv", "tag",
    };

    private static readonly HashSet<string> WriteRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", "exec",
    };

    private static readonly string[] Denied =
    {
        "--upload-pack", "--receive-pack", "--exec=", "upload-archive",
        "push", "reset", "clean", "rebase", "merge", "checkout", "filter-branch", "--force",
    };

    /// <inheritdoc />
    public ToolResult Handle(string argumentsJson, ToolContext context)
    {
        JsonObject? obj;
        try { obj = JsonNode.Parse(argumentsJson)?.AsObject(); }
        catch (Exception ex) { return ToolResult.Fail($"argumentos invalidos: {ex.Message}"); }
        if (obj is null) return ToolResult.Fail("argumentos invalidos: objeto JSON esperado");

        string command = obj["command"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(command))
            return ToolResult.Fail("command e obrigatorio (ex.: 'git status')");

        string repoDir = context.WorkspaceRoot;
        string wantPath = obj["path"]?.GetValue<string>() ?? "";
        if (!string.IsNullOrWhiteSpace(wantPath))
        {
            if (!WorkspaceGuard.TryResolve(context.WorkspaceRoot, wantPath, out string resolved,
                    out string whyPath, context.ExtraRoots))
                return ToolResult.Fail(whyPath);
            repoDir = resolved;
        }

        if (Validate(command, context.Role, out string[] argv) is string why)
            return ToolResult.Fail(why);

        try
        {
            var r = ProcessRunner.Run(argv[0], argv[1..], repoDir, context.TimeoutSeconds);
            return r.Success ? ToolResult.Ok(r.Output) : ToolResult.Fail(r.Error ?? "falha");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"falha ao rodar git: {ex.Message.Split('\n')[0]}");
        }
    }

    /// <summary>Valida o comando git para o role; null libera e devolve o argv.</summary>
    /// <param name="command">Comando informado pelo modelo.</param>
    /// <param name="role">Role do agente (<c>build</c>/<c>exec</c> escrevem).</param>
    /// <param name="argv">Argumentos normalizados (com <c>git</c> na frente).</param>
    /// <returns>Motivo da negacao ou null.</returns>
    public static string? Validate(string command, string? role, out string[] argv)
    {
        argv = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(command))
            return "command e obrigatorio (ex.: 'git status')";

        bool canWrite = WriteRoles.Contains(role ?? "");
        var allowed = canWrite
            ? new HashSet<string>(ReadAllowed.Concat(WriteAllowed), StringComparer.Ordinal)
            : ReadAllowed;
        string allowedList = string.Join(", ", allowed);

        string norm = command.Replace('\t', ' ');
        foreach (string bad in Denied)
        {
            if (norm.Contains(bad, StringComparison.Ordinal))
                return $"git negado: '{bad.Trim()}' nao e permitido (escrita local via init/add/commit)";
        }

        // -c e aceito so para a identidade do commit (user.name/user.email).
        foreach (Match m in Regex.Matches(norm, @"(?:^|\s)-c\s+(\S+)"))
        {
            string kv = m.Groups[1].Value;
            if (!kv.StartsWith("user.name=", StringComparison.Ordinal)
                && !kv.StartsWith("user.email=", StringComparison.Ordinal))
                return "git -c: so user.name/user.email sao aceitos";
        }

        string[] parsed = Split(command);
        if (parsed.Length > 0 && parsed[0] != "git" && allowed.Contains(parsed[0]))
            parsed = new[] { "git" }.Concat(parsed).ToArray();
        if (parsed.Length == 0 || parsed[0] != "git")
            return $"command deve comecar com 'git' (role '{role}' permite: {allowedList})";
        if (parsed.Length < 2 || !allowed.Contains(parsed[1]))
            return $"subcomando git nao permitido para o role '{role}' (permitidos: {allowedList})";

        argv = parsed;
        return null;
    }

    private static string[] Split(string command)
    {
        var parts = new List<string>();
        var cur = new System.Text.StringBuilder();
        char quote = '\0';
        foreach (char ch in command.Trim())
        {
            if (quote != '\0')
            {
                if (ch == quote) quote = '\0';
                else cur.Append(ch);
            }
            else if (ch is '"' or '\'') quote = ch;
            else if (char.IsWhiteSpace(ch))
            {
                if (cur.Length > 0) { parts.Add(cur.ToString()); cur.Clear(); }
            }
            else cur.Append(ch);
        }
        if (cur.Length > 0) parts.Add(cur.ToString());
        return parts.ToArray();
    }
}
