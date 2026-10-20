namespace Rochas.AI.Manager.Application.Helpers;

/// <summary>
/// Concilia o CSV de tools do banco com o do seed. O token legado `task` vira
/// `subagent` (nome real da tool no opencode 2.x), `subagent` entra nas roles
/// que delegam e sai de quem nao delega (Exec). Tools extras do usuario ficam.
/// </summary>
public static class ToolListNormalizer
{
    /// <summary>Nome antigo (nao existe mais no array de tools do opencode).</summary>
    public const string LegacySubagentToken = "task";

    /// <summary>Nome real da tool de delegacao.</summary>
    public const string SubagentToken = "subagent";

    public static string Normalize(string? current, string seeded, bool delegating)
    {
        // Sem nada no banco (primeiro boot): o seed manda, na ordem canonica.
        if (string.IsNullOrWhiteSpace(current)) return seeded;

        var tokens = new List<string>();
        foreach (var raw in current.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = raw.Equals(LegacySubagentToken, StringComparison.OrdinalIgnoreCase) ? SubagentToken : raw;
            if (!Contains(tokens, token)) tokens.Add(token);
        }
        if (delegating && !Contains(tokens, SubagentToken)) tokens.Add(SubagentToken);
        if (!delegating) tokens.RemoveAll(token => token.Equals(SubagentToken, StringComparison.OrdinalIgnoreCase));
        return string.Join(",", tokens);
    }

    public static bool HasToken(string? csv, string token)
        => (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(item => item.Equals(token, StringComparison.OrdinalIgnoreCase));

    public static string JoinToken(string? csv, string token)
        => string.IsNullOrWhiteSpace(csv) ? token : csv.TrimEnd(',') + "," + token;

    public static string RemoveToken(string? csv, string token)
        => string.Join(",", (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !item.Equals(token, StringComparison.OrdinalIgnoreCase)));

    private static bool Contains(List<string> tokens, string token)
        => tokens.Any(item => item.Equals(token, StringComparison.OrdinalIgnoreCase));
}
