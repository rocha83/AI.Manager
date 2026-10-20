using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Extrai tool call escrita no TEXTO (modelos sem tool calling nativo ou que
/// narram a chamada). Cobre os formatos que a bridge antiga reconhecia:
/// &lt;tool_call&gt;/&lt;tools&gt;/&lt;function&gt;, bloco cerrado ```json,
/// formato de ângulo &lt;{"name":...}&gt; e "[Assistant tool call]: nome({...})".
/// </summary>
public static class ToolCallTextExtractor
{
    private static readonly Regex ReFenced = new(@"```(?:json)?\s*(\{\s*""name""\s*:[\s\S]*?\})\s*```", RegexOptions.Compiled);
    private static readonly Regex ReToolTag = new(@"<tool_call>([\s\S]*?)</tool_call\|?>", RegexOptions.Compiled);
    private static readonly Regex ReToolsTag = new(@"<tools>([\s\S]*?)</tools>", RegexOptions.Compiled);
    private static readonly Regex ReFunctionTag = new(@"<function>([\s\S]*?)</function>", RegexOptions.Compiled);
    private static readonly Regex ReAssistant = new(@"\[Assistant tool call\]:\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<(string Name, string Arguments)> Extract(string? content, IReadOnlyList<string>? known = null)
    {
        var calls = new List<(string, string)>();
        if (string.IsNullOrEmpty(content)) return calls;

        foreach (Match match in ReToolTag.Matches(content)) TryAdd(calls, match.Groups[1].Value, known);
        if (calls.Count == 0) foreach (Match match in ReToolsTag.Matches(content)) TryAdd(calls, match.Groups[1].Value, known);
        if (calls.Count == 0) foreach (Match match in ReFunctionTag.Matches(content)) TryAdd(calls, match.Groups[1].Value, known);
        if (calls.Count == 0) foreach (Match match in ReFenced.Matches(content)) TryAdd(calls, match.Groups[1].Value, known);

        // Formato de ângulo <{"name":...}> (chaves balanceadas)
        if (calls.Count == 0)
        {
            int index = 0;
            while (index < content.Length)
            {
                int open = content.IndexOf('<', index);
                if (open < 0 || open + 1 >= content.Length) break;
                int start = open + 1;
                if (content[start] != '{') { index = start; continue; }
                if (TryBalanced(content, start, out string json) && json.Contains("\"name\""))
                {
                    TryAdd(calls, json, known);
                    index = start + json.Length;
                }
                else index = start + 1;
            }
        }

        // "[Assistant tool call]: nome({...})"
        if (calls.Count == 0)
        {
            foreach (Match match in ReAssistant.Matches(content))
            {
                int start = match.Index + match.Length;
                if (start >= content.Length || content[start] != '{') continue;
                if (!TryBalanced(content, start, out string arguments)) continue;
                var envelope = new JsonObject
                {
                    ["name"] = match.Groups[1].Value,
                    ["arguments"] = JsonNode.Parse(arguments)
                };
                TryAdd(calls, envelope.ToJsonString(), known);
            }
        }

        // Último recurso: objeto cru no início do texto.
        if (calls.Count == 0)
        {
            string trimmed = content.TrimStart();
            if (trimmed.StartsWith("{\"name\"", StringComparison.Ordinal) && trimmed.Contains("\"arguments\""))
                if (TryBalanced(trimmed, 0, out string json)) TryAdd(calls, json, known);
        }

        return calls;
    }

    private static void TryAdd(List<(string Name, string Arguments)> calls, string json, IReadOnlyList<string>? known)
    {
        try
        {
            if (JsonNode.Parse(json) is not JsonObject node) return;
            string? name = node["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (known is { Count: > 0 } && !known.Contains(name, StringComparer.OrdinalIgnoreCase)) return;
            string arguments = node["arguments"] is null ? "{}" : JsonOps.ToArguments(node["arguments"]);
            calls.Add((name, arguments));
        }
        catch { }
    }

    private static bool TryBalanced(string text, int start, out string json)
    {
        json = "";
        int depth = 0;
        bool inString = false, escaped = false;
        for (int index = start; index < text.Length; index++)
        {
            char c = text[index];
            if (inString)
            {
                if (escaped) escaped = false;
                else if (c == '\\') escaped = true;
                else if (c == '"') inString = false;
                continue;
            }
            if (c == '"') inString = true;
            else if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    json = text.Substring(start, index - start + 1);
                    return true;
                }
            }
        }
        return false;
    }
}
