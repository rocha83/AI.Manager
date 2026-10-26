using System.Text;
using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Transport;

/// <summary>
/// Shim generico de tool calling para upstreams que recusam grammar customizada
/// junto com `tools` (llama.cpp: "Cannot use custom grammar constraints with tools").
///
/// Remove o array `tools` do request, injeta a declaracao no system e forca um
/// envelope JSON via grammar. A resposta e convertida de volta para o formato
/// OpenAI `tool_calls`. Nao conhece cenario: usa os nomes e schemas do proprio
/// request, entao serve para write/read/grep/shell/subagent e qualquer outro.
/// </summary>
public static class ToolShim
{
    /// <summary>
    /// Aplica o shim no body de /v1/chat/completions (mutacao no lugar).
    /// Requer tools com nome e ao menos um tool; forca o upstream non-stream
    /// (o cliente pode ter pedido stream=true: a bridge sintetiza o SSE).
    /// </summary>
    public static bool Apply(JsonObject body, int maxString, int minCalls, int maxTokens, out string? reason)
    {
        reason = null;
        if (body["tools"] is not JsonArray tools || tools.Count == 0)
        {
            reason = "sem tools";
            return false;
        }

        var names = new List<string>();
        foreach (var tool in tools)
        {
            if (tool is JsonObject obj
                && obj["function"]?["name"]?.GetValue<string>() is string name
                && !string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }
        if (names.Count == 0)
        {
            reason = "tools sem nome";
            return false;
        }

        // minCalls: enquanto o historico nao tiver N tool calls, o grammar NAO
        // oferece a saida {"final":...}. Isso impede o modelo de encerrar com
        // texto antes de construir os artefatos; depois de N calls, final libera.
        var calls = 0;
        if (body["messages"] is JsonArray messages)
        {
            foreach (var message in messages)
            {
                if (message is JsonObject obj
                    && obj["role"]?.GetValue<string>() == "assistant"
                    && obj["tool_calls"] is JsonArray toolCalls)
                {
                    calls += toolCalls.Count;
                }
            }
        }

        var allowFinal = calls >= minCalls;
        body["grammar"] = BuildGrammar(tools, maxString, allowFinal);
        AppendSystem(body, BuildSystemPrompt(tools, allowFinal));
        body.Remove("tools");
        body.Remove("tool_choice");
        body.Remove("parallel_tool_calls");
        body["stream"] = false;
        body.Remove("stream_options");

        if (maxTokens > 0)
        {
            body["max_tokens"] = maxTokens;
            body.Remove("max_completion_tokens");
        }
        body["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = false };
        return true;
    }

    /// <summary>
    /// Converte o content JSON do modelo em tool_calls OpenAI (ou em content
    /// final). Se nao for JSON reconhecivel, devolve a resposta como veio.
    /// </summary>
    public static JsonObject TransformResponse(JsonObject response)
    {
        if (response["choices"] is not JsonArray choices || choices.Count == 0) return response;
        if (choices[0] is not JsonObject choice || choice["message"] is not JsonObject message) return response;

        var content = message["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(content)) return response;

        JsonObject? parsed = null;
        try { parsed = JsonNode.Parse(content.Trim())?.AsObject(); }
        catch { }
        if (parsed is null) return response;

        if (parsed["name"]?.GetValue<string>() is string name && parsed["arguments"] is JsonNode args)
        {
            var argJson = args is JsonObject ? args.ToJsonString() : args.GetValue<string>() ?? "{}";
            var call = new JsonObject
            {
                ["id"] = "call_" + Guid.NewGuid().ToString("N"),
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = name, ["arguments"] = argJson }
            };
            message["content"] = "";
            message["tool_calls"] = new JsonArray(call);
            choice["finish_reason"] = "tool_calls";
            return response;
        }

        if (parsed["final"]?.GetValue<string>() is string final)
        {
            message["content"] = final;
            choice["finish_reason"] = "stop";
        }
        return response;
    }

    private static void AppendSystem(JsonObject body, string text)
    {
        if (body["messages"] is not JsonArray messages) return;

        for (var i = 0; i < messages.Count; i++)
        {
            if (messages[i] is not JsonObject message) continue;
            var role = message["role"]?.GetValue<string>();
            if (role is not ("system" or "developer")) continue;

            if (message["content"] is JsonValue value && value.TryGetValue<string>(out var current))
            {
                message["content"] = current.TrimEnd() + "\n\n" + text;
                return;
            }
            if (message["content"] is JsonArray parts)
            {
                parts.Add(new JsonObject { ["type"] = "text", ["text"] = text });
                return;
            }
        }

        messages.Insert(0, new JsonObject { ["role"] = "system", ["content"] = text });
    }

    private static string BuildSystemPrompt(JsonArray tools, bool allowFinal)
    {
        var json = tools.ToJsonString();
        return
            "[TOOL SHIM JSON]\n" +
            "Ferramentas disponiveis (nome, descricao, parametros JSON):\n" +
            json + "\n" +
            "Responda SOMENTE com um objeto JSON, sem markdown e sem texto fora:\n" +
            "- Para chamar uma ferramenta: {\"name\":\"<nome>\",\"arguments\":{...}}\n" +
            (allowFinal
                ? "- Para encerrar sem ferramenta: {\"final\":\"<texto>\"}\n"
                : "- OBRIGATORIO chamar uma ferramenta agora; nao use final.\n") +
            "Use exatamente os nomes e parametros declarados. Nao invente ferramenta. " +
            "Se o usuario pedir para ler, revisar ou inspecionar arquivos, chame a ferramenta de leitura antes de finalizar.";
    }

    private static string BuildGrammar(JsonArray tools, int maxString, bool allowFinal)
    {
        var defs = new List<(string Name, List<(string Key, string Rule)> Required)>();
        foreach (var tool in tools)
        {
            if (tool is not JsonObject obj || obj["function"] is not JsonObject function) continue;
            var name = function["name"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var required = new List<(string Key, string Rule)>();
            if (function["parameters"] is JsonObject parameters
                && parameters["properties"] is JsonObject properties
                && parameters["required"] is JsonArray requiredNames)
            {
                foreach (var requiredName in requiredNames)
                {
                    var key = requiredName?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    required.Add((key, TypeRule(properties[key] as JsonObject)));
                }
            }
            defs.Add((name, required));
        }
        if (defs.Count == 0) return "";

        var builder = new StringBuilder();
        builder.Append(allowFinal ? "root ::= ws? (call | final) ws?\n" : "root ::= ws? call ws?\n");
        builder.Append("call ::= ");
        for (var i = 0; i < defs.Count; i++)
        {
            if (i > 0) builder.Append(" | ");
            var (name, required) = defs[i];
            builder.Append("\"{\" ws ").Append(GbnfJsonString("name")).Append(" ws \":\" ws ")
                .Append(GbnfJsonString(name)).Append(" ws \",\" ws ")
                .Append(GbnfJsonString("arguments")).Append(" ws \":\" ws ");

            if (required.Count == 0)
            {
                builder.Append("object");
            }
            else
            {
                builder.Append("\"{\" ws ");
                for (var j = 0; j < required.Count; j++)
                {
                    if (j > 0) builder.Append("ws \",\" ws ");
                    builder.Append(GbnfJsonString(required[j].Key)).Append(" ws \":\" ws ")
                        .Append(required[j].Rule).Append(" ws ");
                }
                builder.Append("\"}\"");
            }
            builder.Append(" ws \"}\"");
        }
        builder.Append('\n');

        if (allowFinal)
        {
            builder.Append("final ::= \"{\" ws ").Append(GbnfJsonString("final"))
                .Append(" ws \":\" ws string ws \"}\"\n");
        }

        builder.Append("object ::= \"{\" ws (pair (ws \",\" ws pair)*)? ws \"}\"\n");
        builder.Append("pair ::= string ws \":\" ws value\n");
        builder.Append("value ::= string | number | boolean | \"null\" | object | array\n");
        builder.Append("boolean ::= \"true\" | \"false\"\n");
        builder.Append("array ::= \"[\" ws (value (ws \",\" ws value)*)? ws \"]\"\n");
        builder.Append("string ::= \"\\\"\" schar{0,").Append(maxString).Append("} \"\\\"\"\n");
        builder.Append(@"schar ::= [^""\\\x00-\x1F] | ""\\"" [""\\/bfnrt] | ""\\u"" [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F] [0-9a-fA-F]" + "\n");
        builder.Append("number ::= \"-\"? [0-9]+ (\".\" [0-9]+)? (\"e\" (\"+\" | \"-\")? [0-9]+)?\n");
        builder.Append("ws ::= [ \\t\\n\\r]{0,2}\n");
        return builder.ToString();
    }

    private static string TypeRule(JsonObject? schema)
    {
        if (schema?["enum"] is JsonArray values && values.Count > 0)
        {
            var parts = new List<string>();
            foreach (var value in values)
            {
                if (value is JsonValue node && node.TryGetValue<string>(out var text))
                    parts.Add(GbnfJsonString(text));
            }
            if (parts.Count > 0) return string.Join(" | ", parts);
        }

        var type = schema?["type"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var typeName)
            ? typeName
            : null;
        return type switch
        {
            "string" => "string",
            "number" or "integer" => "number",
            "boolean" => "boolean",
            "array" => "array",
            "object" => "object",
            _ => "value"
        };
    }

    private static string GbnfJsonString(string value) => "\"" + EscapeGbnf("\"" + value + "\"") + "\"";

    private static string EscapeGbnf(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
}
