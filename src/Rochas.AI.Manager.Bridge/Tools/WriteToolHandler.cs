using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Handler da tool <c>write</c>: grava arquivo de texto (cria diretorios pais).
/// O conteudo e sempre validado pelo jail do workspace.
/// </summary>
public sealed class WriteToolHandler : IToolHandler
{
    /// <inheritdoc />
    public string ToolName => "write";

    private const int MaxChars = 1_000_000;

    /// <inheritdoc />
    public ToolResult Handle(string argumentsJson, ToolContext context)
    {
        JsonObject? obj;
        try { obj = JsonNode.Parse(argumentsJson)?.AsObject(); }
        catch (Exception ex) { return ToolResult.Fail($"argumentos invalidos: {ex.Message}"); }
        if (obj is null) return ToolResult.Fail("argumentos invalidos: objeto JSON esperado");

        string path = obj["path"]?.GetValue<string>() ?? "";
        string content = obj["content"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(path)) return ToolResult.Fail("path e obrigatorio");
        if (content.Length > MaxChars)
            return ToolResult.Fail($"conteudo grande demais ({content.Length} chars, teto {MaxChars})");

        if (!WorkspaceGuard.TryResolve(context.WorkspaceRoot, path, out string full, out string why, context.ExtraRoots))
            return ToolResult.Fail(why);

        try
        {
            string? dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            bool existed = File.Exists(full);
            File.WriteAllText(full, content);
            return ToolResult.Ok($"{(existed ? "atualizado" : "criado")} {content.Length} chars em '{path}'");
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"falha ao escrever '{path}': {ex.Message}");
        }
    }
}
