using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Tradução chat -> Responses (não-stream): thinking vira item reasoning,
/// content vira message, tool_calls viram function_call. É o mesmo objeto que o
/// evento response.completed devolve no stream.
/// </summary>
public static class ChatToResponses
{
    private static readonly Regex ReTemplate = new(@"<\|im_(start|end)\|>", RegexOptions.Compiled);
    private static readonly Regex ReFencedCall = new(@"```(?:json)?\s*(\{\s*""name""\s*:[\s\S]*?\})\s*```", RegexOptions.Compiled);
    private static readonly Regex ReToolTag = new(@"<tool_call>([\s\S]*?)</tool_call\|?>", RegexOptions.Compiled);
    private static readonly Regex ReToolsTag = new(@"<tools>([\s\S]*?)</tools>", RegexOptions.Compiled);
    private static readonly Regex ReFunctionTag = new(@"<function>([\s\S]*?)</function>", RegexOptions.Compiled);
    private static readonly Regex ReAssistantToolCall = new(
        @"\[Assistant tool call\]:\s*([A-Za-z_][A-Za-z0-9_.]*)\s*\(", RegexOptions.Compiled);

    public static JsonObject Convert(
        JsonObject chat,
        string model,
        double temperature,
        BridgeOptions options,
        string? thinkMode = null,
        bool hadTools = false,
        IReadOnlyList<string>? announcedTools = null,
        JsonNode? historyMessages = null)
    {
        thinkMode ??= options.Thinking;
        if (options.QwenStandard) QwenStandard.TranslateCompletionForClient(chat);
        var choice = chat["choices"]?[0]?.AsObject() ?? new JsonObject();
        var message = choice["message"]?.AsObject() ?? new JsonObject();

        string content = message["content"]?.GetValue<string>() ?? "";
        string thinking = "";

        if ((message["reasoning_content"] ?? message["reasoning"]) is JsonValue reasoningValue
            && reasoningValue.TryGetValue<string>(out string? reasoningText))
        {
            thinking = reasoningText ?? "";
        }
        var (thinkingBlock, cleanContent) = JsonOps.SplitThinking(content);
        if (thinkingBlock.Length > thinking.Length) thinking = thinkingBlock;
        content = ReTemplate.Replace(cleanContent, "");
        thinking = ControlTags.Strip(thinking);

        var toolCalls = new List<(string Id, string Name, string Arguments)>();
        string notice = "";

        var loopedSignatures = options.AntiRepeat
            ? AntiRepeat.LoopedToolSignatures(historyMessages, 2)
            : new HashSet<string>(StringComparer.Ordinal);

        if (message["tool_calls"] is JsonArray callArray)
        {
            foreach (var callNode in callArray)
            {
                if (callNode is not JsonObject callObj) continue;
                string id = callObj["id"]?.GetValue<string>() ?? ("call_" + JsonOps.NewId());
                var function = callObj["function"]?.AsObject() ?? new JsonObject();
                string name = function["name"]?.GetValue<string>() ?? "";
                // Alguns clientes mandam "default.nome" (namespace da tool).
                if (name.StartsWith("default.", StringComparison.OrdinalIgnoreCase))
                    name = name["default.".Length..];
                name = ResolveAnnouncedName(ResponsesTranslation.CanonToolAlias(name), announcedTools);
                string arguments = JsonOps.ToArguments(function["arguments"]);

                var argsProbe = new ToolArgsAntiRepeatReply(options.AntiRepeatWindow);
                bool argsLooped = options.AntiRepeat
                    && arguments.Length >= ToolArgsAntiRepeatReply.ProbeMinChars
                    && argsProbe.IsRepeat(arguments);
                if (argsLooped || loopedSignatures.Contains(AntiRepeat.ToolSignature(name, arguments)))
                {
                    if (notice.Length == 0) notice = "\n\n" + (options.AntiRepeatNotice ?? AntiRepeat.DefaultNotice) + "\n\n";
                    continue;
                }
                toolCalls.Add((id, name, arguments));
            }
        }

        // Coerção: sem tool_calls nativos e com tools anunciadas, extrai do texto
        // (hermes/plural/fence). Só atua se o texto parsear; prosa passa intacta.
        if (toolCalls.Count == 0 && options.CoerceNarrated && hadTools)
        {
            var known = announcedTools?.ToList() ?? new List<string>();
            if (options.QwenStandard)
            {
                foreach (string name in known.ToList())
                {
                    string upstream = QwenStandard.ToUpstreamName(name);
                    if (!known.Contains(upstream, StringComparer.OrdinalIgnoreCase)) known.Add(upstream);
                }
            }
            foreach (var (name, arguments) in ToolCallTextExtractor.Extract(content, known))
            {
                var (clientName, clientArgs) = options.QwenStandard
                    ? QwenStandard.ToClient(name, arguments)
                    : (name, arguments);
                toolCalls.Add(("call_" + JsonOps.NewId(), clientName, clientArgs));
            }

            if (toolCalls.Count > 0) content = StripToolBlocks(content).Trim();
        }

        int inputTokens = chat["usage"]?["prompt_tokens"]?.GetValue<int>() ?? 0;
        int outputTokens = chat["usage"]?["completion_tokens"]?.GetValue<int>() ?? 0;
        if (outputTokens == 0) outputTokens = Math.Max(content.Length / 4 + toolCalls.Count * 8, 1);

        var output = new JsonArray();

        // Modo "off": sem item reasoning; o raciocínio vira texto comum (senão a
        // resposta sai vazia quando o modelo pensa tudo e não escreve nada fora).
        if (thinkMode != "events" && !string.IsNullOrWhiteSpace(thinking))
            content = string.IsNullOrEmpty(content) ? thinking : thinking + "\n\n" + content;

        if (thinkMode == "events" && !string.IsNullOrWhiteSpace(thinking))
        {
            output.Add(new JsonObject
            {
                ["id"] = "rs_" + JsonOps.NewId(),
                ["summary"] = new JsonArray(),
                ["type"] = "reasoning",
                ["content"] = new JsonArray(new JsonObject { ["text"] = thinking }),
                ["encrypted_content"] = "",
                ["status"] = "completed"
            });
        }

        if (!string.IsNullOrWhiteSpace(content) || notice.Length > 0)
        {
            output.Add(new JsonObject
            {
                ["id"] = "msg_" + JsonOps.NewId(),
                ["type"] = "message",
                ["role"] = "assistant",
                ["status"] = "completed",
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "output_text",
                    ["text"] = content + notice,
                    ["annotations"] = new JsonArray(),
                    ["logprobs"] = null
                })
            });
        }

        foreach (var (id, name, arguments) in toolCalls)
        {
            output.Add(new JsonObject
            {
                ["type"] = "function_call",
                ["id"] = id,
                ["call_id"] = id,
                ["name"] = name,
                ["arguments"] = arguments
            });
        }

        return NewResponse(output, model, temperature, inputTokens, outputTokens, "completed");
    }

    /// <summary>Resposta sintetica do breaker (sem chamada ao upstream).</summary>
    public static JsonObject TripResponse(string notice, string model, double temperature)
    {
        var output = new JsonArray(new JsonObject
        {
            ["id"] = "msg_" + JsonOps.NewId(),
            ["type"] = "message",
            ["role"] = "assistant",
            ["status"] = "completed",
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "output_text",
                ["text"] = notice,
                ["annotations"] = new JsonArray(),
                ["logprobs"] = null
            })
        });
        return NewResponse(output, model, temperature,
            inputTokens: 0, outputTokens: Math.Max(1, notice.Length / 4), "completed");
    }

    /// <summary>
    /// Mantem o nome que o cliente anunciou quando o apelido canonico casa
    /// (ex.: cliente anunciou "bash"; o modelo devolveu "bash" ou "shell").
    /// </summary>
    private static string ResolveAnnouncedName(string canonical, IReadOnlyList<string>? announced)
    {
        if (announced is not { Count: > 0 }) return canonical;
        foreach (string name in announced)
        {
            if (string.Equals(ResponsesTranslation.CanonToolAlias(name), canonical, StringComparison.OrdinalIgnoreCase))
                return name;
        }
        return canonical;
    }

    /// <summary>Objeto Responses base (usado no completed e no não-stream).</summary>
    public static JsonObject NewResponse(JsonArray output, string model, double temperature,
        int inputTokens, int outputTokens, string status) => new()
    {
        ["id"] = "resp_" + JsonOps.NewId(),
        ["object"] = "response",
        ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ["status"] = status,
        ["model"] = model,
        ["output"] = output,
        ["parallel_tool_calls"] = true,
        ["tool_choice"] = "auto",
        ["tools"] = new JsonArray(),
        ["temperature"] = temperature,
        ["service_tier"] = "auto",
        ["incomplete_details"] = null,
        ["instructions"] = null,
        ["metadata"] = null,
        ["usage"] = new JsonObject
        {
            ["input_tokens"] = inputTokens,
            ["output_tokens"] = outputTokens,
            ["total_tokens"] = inputTokens + outputTokens,
            ["input_tokens_details"] = new JsonObject { ["cached_tokens"] = 0 },
            ["output_tokens_details"] = new JsonObject()
        }
    };

    internal static string StripToolBlocks(string content)
    {
        var text = ReToolTag.Replace(content, "");
        text = ReToolsTag.Replace(text, "");
        text = ReFunctionTag.Replace(text, "");
        text = ReFencedCall.Replace(text, "");
        return ReAssistantToolCall.Replace(text, "");
    }

    internal static bool HasNarratedCall(string content) => ReAssistantToolCall.IsMatch(content);

    internal static string RemoveTemplateMarkers(string content) => ReTemplate.Replace(content, "");
}
