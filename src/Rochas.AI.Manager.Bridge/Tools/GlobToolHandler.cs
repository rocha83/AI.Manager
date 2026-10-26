using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Handler da tool <c>glob</c>: lista caminhos por padrao glob sem shell.
/// Aceita <c>**</c> como recursao e padrao absoluto.
/// </summary>
public sealed class GlobToolHandler : IToolHandler
{
    /// <inheritdoc />
    public string ToolName => "glob";

    private const int MaxResults = 500;

    /// <inheritdoc />
    public ToolResult Handle(string argumentsJson, ToolContext context)
    {
        JsonObject? obj;
        try { obj = JsonNode.Parse(argumentsJson)?.AsObject(); }
        catch (Exception ex) { return ToolResult.Fail($"argumentos invalidos: {ex.Message}"); }
        if (obj is null) return ToolResult.Fail("argumentos invalidos: objeto JSON esperado");

        string pattern = obj["pattern"]?.GetValue<string>() ?? "";
        if (string.IsNullOrWhiteSpace(pattern)) return ToolResult.Fail("pattern e obrigatorio");
        string subdir = obj["path"]?.GetValue<string>() ?? "";

        if (!WorkspaceGuard.TryResolve(context.WorkspaceRoot, subdir, out string baseDir, out string why, context.ExtraRoots))
            return ToolResult.Fail(why);

        // "**" e recursao (AllDirectories ja desce): "src/**" vira busca "*" em
        // src, "**/*.cs" vira "*.cs" na raiz.
        string pat = pattern.Trim();
        string searchDir = baseDir;

        // Padrao absoluto ("/tmp/x/*.cs"): separa o diretorio do nome do arquivo.
        if (Path.IsPathRooted(pat))
        {
            int wild = pat.IndexOfAny(new[] { '*', '?' });
            string dirPart = wild < 0 ? pat : pat[..wild];
            dirPart = dirPart.TrimEnd('/', '\\');
            if (dirPart.Length == 0) dirPart = Path.GetPathRoot(pat) ?? "/";
            pat = wild < 0 ? "*" : pat[wild..].TrimStart('/', '\\');
            baseDir = dirPart;
            searchDir = dirPart;
        }

        if (!Directory.Exists(baseDir)) return ToolResult.Fail($"diretorio nao encontrado: '{subdir}'");
        int star = pat.IndexOf("**", StringComparison.Ordinal);
        if (star >= 0)
        {
            string extra = pat[..star].Trim().TrimEnd('/', '\\');
            string tail = pat[(star + 2)..].Trim().TrimStart('/', '\\');
            pat = string.IsNullOrWhiteSpace(tail) ? "*" : tail;
            if (!string.IsNullOrWhiteSpace(extra) && extra.IndexOf('*') < 0)
            {
                searchDir = Path.Combine(baseDir, extra);
                if (!Directory.Exists(searchDir))
                    return ToolResult.Fail($"diretorio nao encontrado: '{subdir}{Path.DirectorySeparatorChar}{extra}'");
            }
        }

        try
        {
            string[] hits = Directory.GetFileSystemEntries(searchDir, pat, SearchOption.AllDirectories);
            string wsRoot = Path.GetFullPath(context.WorkspaceRoot);
            string[] rel = hits
                .Select(h =>
                {
                    string r = Path.GetRelativePath(wsRoot, h);
                    // Fora do workspace: relativo vira "../../.." inutil — devolve absoluto.
                    return r.StartsWith("..", StringComparison.Ordinal) ? Path.GetFullPath(h) : r;
                })
                .OrderBy(h => h, StringComparer.Ordinal)
                .Take(MaxResults)
                .ToArray();
            return ToolResult.Ok(rel.Length == 0
                ? $"(sem resultados em '{(string.IsNullOrWhiteSpace(subdir) ? "." : subdir)}'; se ja listou arquivos antes, leia o conteudo deles com read)"
                : string.Join('\n', rel));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"falha no glob: {ex.Message}");
        }
    }
}
