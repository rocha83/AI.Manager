using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge;

/// <summary>Utilidades de JSON/texto compartilhadas pela tradução e pelo stream.</summary>
public static class JsonOps
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    public static string NewId() => Guid.NewGuid().ToString("N")[..16];

    /// <summary>Extrai texto de nó string, lista de partes ou objeto com "text".</summary>
    public static string ExtractText(JsonNode? node)
    {
        if (node is null) return "";
        if (node is JsonValue value)
            return value.TryGetValue<string>(out string? text) ? text : value.ToJsonString(SerializerOptions);
        if (node is JsonArray parts)
        {
            var builder = new StringBuilder();
            foreach (var part in parts)
            {
                if (part is JsonValue partValue && partValue.TryGetValue<string>(out string? partText))
                {
                    builder.Append(partText);
                    continue;
                }
                if (part is JsonObject partObj && partObj["text"] is JsonValue textNode
                    && textNode.TryGetValue<string>(out string? nested))
                {
                    builder.Append(nested);
                }
            }
            return builder.ToString();
        }
        if (node is JsonObject obj && obj["text"] is JsonValue textValue
            && textValue.TryGetValue<string>(out string? objText))
        {
            return objText;
        }
        return node.ToJsonString(SerializerOptions);
    }

    /// <summary>Argumentos de tool call: string passa direto, nulo vira "{}".</summary>
    public static string ToArguments(JsonNode? node) =>
        node is null ? "{}"
        : node is JsonValue value && value.TryGetValue<string>(out string? text) ? text
        : node.ToJsonString(SerializerOptions);

    /// <summary>JsonNode só aceita um pai: clona ou devolve schema vazio.</summary>
    public static JsonNode CloneOrDefault(JsonNode? node) =>
        node is null ? new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() } : node.DeepClone();

    /// <summary>
    /// Separa o bloco &lt;think&gt;...&lt;/think&gt; do texto final. Sem tag de
    /// fechamento (corte por max_tokens), tudo depois da abertura é raciocínio.
    /// </summary>
    public static (string Thinking, string Clean) SplitThinking(string? text)
    {
        if (string.IsNullOrEmpty(text)) return ("", "");
        int open = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        if (open < 0) return ("", text);
        int close = text.IndexOf("</think>", open, StringComparison.OrdinalIgnoreCase);
        if (close < 0)
            return (ControlTags.Strip(text[(open + 7)..]).Trim(), text[..open].Trim());

        string before = text[..open];
        string thought = ControlTags.Strip(text[(open + 7)..close]);
        string after = text[(close + 8)..];
        return (thought.Trim(), (before + after).Trim());
    }

    /// <summary>Normaliza caixa/espaços para comparar repetição.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var builder = new StringBuilder(text.Length);
        bool space = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!space) { builder.Append(' '); space = true; }
            }
            else
            {
                builder.Append(char.ToLowerInvariant(c));
                space = false;
            }
        }
        return builder.ToString();
    }

    public static JsonArray CloneArrayOrEmpty(JsonNode? node) =>
        node is JsonArray array ? (JsonArray)array.DeepClone() : new JsonArray();
}
