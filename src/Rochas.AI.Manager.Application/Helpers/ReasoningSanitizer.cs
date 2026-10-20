using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Application.Helpers;

public static class ReasoningSanitizer
{
    // Mesmas tags que a bridge remove no stream. O conteudo de reasoning NAO e
    // parse: se a tag vazar para o cliente, o pensamento aparece poluido.
    private static readonly string[] ControlTags =
        { "<|channel>", "<channel|>", "<|channel|>", "<thought>", "</thought>", "<thinking>", "</thinking>" };

    /// <summary>Remove as tags de controle que o modelo eventualmente emite no texto.</summary>
    public static string StripControlTags(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
        foreach (var tag in ControlTags)
            if (text.Contains(tag, StringComparison.OrdinalIgnoreCase))
                text = text.Replace(tag, string.Empty, StringComparison.OrdinalIgnoreCase);
        return text;
    }

    /// <summary>
    /// Limpa uma resposta Responses: tira as tags de controle do raciocinio
    /// (item type=reasoning, em content[].text e summary[].text). O resto do
    /// payload passa intacto, inclusive o shape nativo do llama
    /// (reasoning_text + item.content).
    /// </summary>
    public static string SanitizeResponses(string json)
    {
        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            if (root is null) return json;
            var output = root["output"]?.AsArray();
            if (output is null) return json;
            foreach (var item in output)
            {
                if (item is not JsonObject obj) continue;
                if (obj["type"]?.GetValue<string>() != "reasoning") continue;
                SanitizeTexts(obj["content"] as JsonArray);
                SanitizeTexts(obj["summary"] as JsonArray);
            }
            return root.ToJsonString();
        }
        catch { return json; }
    }

    private static void SanitizeTexts(JsonArray? parts)
    {
        if (parts is null) return;
        foreach (var part in parts)
        {
            if (part is not JsonObject p) continue;
            if (p["text"]?.GetValue<string>() is string text)
                p["text"] = StripControlTags(text);
        }
    }

    /// <summary>Remove itens de reasoning (type=reasoning) de uma resposta chat, retornando o JSON limpo.</summary>
    public static string Sanitize(string json)
    {
        try
        {
            var root = JsonNode.Parse(json)?.AsObject();
            if (root is null) return json;
            var choices = root["choices"]?.AsArray();
            if (choices is null) return json;
            foreach (var c in choices)
            {
                var msg = c?["message"]?.AsObject();
                if (msg is null) continue;
                // remove reasoning_content field
                msg.Remove("reasoning_content");
                msg.Remove("reasoning");
                // remove reasoning items from content array if present
                if (msg["content"] is JsonArray arr)
                {
                    var filtered = new JsonArray();
                    foreach (var item in arr)
                    {
                        var t = item?["type"]?.GetValue<string>();
                        if (t == "reasoning") continue;
                        filtered.Add(item?.DeepClone());
                    }
                    msg["content"] = filtered;
                }
            }
            return root.ToJsonString();
        }
        catch { return json; }
    }
}