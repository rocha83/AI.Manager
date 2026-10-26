using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Handler da tool <c>edit</c>: substitui texto exato (unico por padrao).
/// Correcao cirurgica; nunca reescreve o arquivo inteiro.
/// </summary>
public sealed class EditToolHandler : IToolHandler
{
    /// <inheritdoc />
    public string ToolName => "edit";

    private const int MaxChars = 1_000_000;

    /// <inheritdoc />
    public ToolResult Handle(string argumentsJson, ToolContext context)
    {
        JsonObject? obj;
        try { obj = JsonNode.Parse(argumentsJson)?.AsObject(); }
        catch (Exception ex) { return ToolResult.Fail($"argumentos invalidos: {ex.Message}"); }
        if (obj is null) return ToolResult.Fail("argumentos invalidos: objeto JSON esperado");

        string path = obj["path"]?.GetValue<string>() ?? "";
        string oldString = obj["oldString"]?.GetValue<string>() ?? "";
        string newString = obj["newString"]?.GetValue<string>() ?? "";
        bool replaceAll = obj["replaceAll"]?.GetValue<bool>() ?? false;
        if (string.IsNullOrWhiteSpace(path)) return ToolResult.Fail("path e obrigatorio");
        if (string.IsNullOrEmpty(oldString)) return ToolResult.Fail("oldString e obrigatorio");

        if (!WorkspaceGuard.TryResolve(context.WorkspaceRoot, path, out string full, out string why, context.ExtraRoots))
            return ToolResult.Fail(why);
        if (!File.Exists(full)) return ToolResult.Fail($"arquivo nao encontrado: '{path}'");

        try
        {
            string text = File.ReadAllText(full);
            if (text.Length > MaxChars)
                return ToolResult.Fail($"arquivo grande demais ({text.Length} chars, teto {MaxChars})");

            int count = CountOccurrences(text, oldString);
            if (count == 0) return ToolResult.Fail("oldString nao encontrado no arquivo");
            if (count > 1 && !replaceAll)
                return ToolResult.Fail($"oldString ocorre {count}x; use replaceAll ou refine o trecho");

            string updated = replaceAll
                ? text.Replace(oldString, newString)
                : ReplaceFirst(text, oldString, newString);
            File.WriteAllText(full, updated);
            return ToolResult.Ok(replaceAll
                ? $"{count} substituicoes em '{path}'"
                : $"1 substituicao em '{path}'");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"falha ao editar '{path}': {ex.Message}");
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0, i = 0;
        while ((i = text.IndexOf(value, i, StringComparison.Ordinal)) >= 0) { count++; i += value.Length; }
        return count;
    }

    private static string ReplaceFirst(string text, string oldValue, string newValue)
    {
        int i = text.IndexOf(oldValue, StringComparison.Ordinal);
        return i < 0 ? text : text[..i] + newValue + text[(i + oldValue.Length)..];
    }
}
