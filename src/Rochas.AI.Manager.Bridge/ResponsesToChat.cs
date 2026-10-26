using System.Text;
using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge;

/// <summary>Resultado da tradução de um request Responses para o corpo chat.</summary>
public sealed class ChatRequestResult
{
    public required JsonObject Body { get; init; }
    public required string RequestedModel { get; init; }
    public required double Temperature { get; init; }
    public required string Thinking { get; init; }
    public bool HadTools { get; init; }
    public IReadOnlyList<string> AnnouncedTools { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DroppedTools { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> PatternHits { get; init; } = Array.Empty<string>();

    /// <summary>Aviso do breaker: quando preenchido, o turno NAO chama o modelo.</summary>
    public string? BreakerTrip { get; init; }

    /// <summary>Mensagens chat do request: historico para detectar chamada repetida.</summary>
    public JsonArray HistoryMessages { get; init; } = new();
}

/// <summary>
/// Tradução Responses -> chat: input (message/function_call/output) vira
/// messages, tools "chatas" viram tools chat, e as caudas de harness entram no
/// system consolidado. Nenhuma decisão de transporte aqui (quem chama monta o
/// HTTP e escolhe o modelo servido).
/// </summary>
public static class ResponsesToChat
{
    public static ChatRequestResult Convert(
        JsonObject request,
        BridgeOptions options,
        string upstreamModel,
        string? serverThinking = null)
    {
        string requestedModel = request["model"]?.GetValue<string>() ?? upstreamModel;
        double temperature = request["temperature"]?.GetValue<double>() ?? options.Temperature;
        string thinking = string.IsNullOrEmpty(serverThinking) ? options.Thinking : serverThinking;

        var messages = InputToMessages(request["input"], request["instructions"], options);
        bool mainApplied = MainHarness.Apply(messages, options.HasProfile, options.MainHarness);
        if (!mainApplied) ApplyTails(messages, options);

        string? breakerTrip = null;
        if (options.BreakerSoft > 0)
        {
            var breaker = new ToolsBreaker(options.BreakerSoft, options.BreakerHard);
            var verdict = breaker.Evaluate(messages);
            if (verdict.Verdict == BreakerVerdict.Trip) breakerTrip = verdict.Notice;
            else if (verdict.Verdict == BreakerVerdict.Nudge) breaker.ApplyNudge(messages, verdict.Notice);
        }

        var tools = ToolsToChat(request["tools"], out var dropped, out _);
        var patternHits = new List<string>();
        if (tools is not null)
        {
            if (options.ToolStrategy) ToolSanitizer.StripExecuteTools(tools, true, "responses");
            if (options.QwenStandard) QwenStandard.TranslateToolsForUpstream(tools);
            StripUnanchoredPatterns(tools, "$", patternHits);
        }

        bool wantThinking = thinking == "events";
        if (request["chat_template_kwargs"]?["enable_thinking"] is JsonValue enabled
            && enabled.TryGetValue<bool>(out bool enabledValue))
        {
            wantThinking = enabledValue;
        }

        var body = new JsonObject
        {
            ["model"] = upstreamModel,
            ["messages"] = messages,
            ["temperature"] = temperature,
            ["stream"] = false,
            ["chat_template_kwargs"] = new JsonObject { ["enable_thinking"] = wantThinking }
        };
        int maxTokens = request["max_output_tokens"] is JsonValue maxNode
            && maxNode.TryGetValue<int>(out int requestedMax) && requestedMax > 0
                ? requestedMax
                : options.MaxTokens;
        if (maxTokens > 0) body["max_tokens"] = maxTokens;
        if (tools is { Count: > 0 })
        {
            body["tools"] = tools;
            body["tool_choice"] = "auto";
        }
        if (request["stop"] is JsonNode stop && HasStop(stop)) body["stop"] = stop.DeepClone();
        if (options.SamplingPassthrough) Sampling.CopyPassthrough(request, body);
        if (options.ToolStrategy) ToolSanitizer.NeutralizeCodeMode(body, true);

        return new ChatRequestResult
        {
            Body = body,
            RequestedModel = requestedModel,
            Temperature = temperature,
            Thinking = thinking,
            HadTools = request["tools"] is JsonArray { Count: > 0 },
            AnnouncedTools = AnnouncedTools(request["tools"] as JsonArray),
            DroppedTools = dropped,
            PatternHits = patternHits,
            BreakerTrip = breakerTrip,
            HistoryMessages = messages
        };
    }

    /// <summary>Converte o input Responses em messages chat (system consolidado no início).</summary>
    public static JsonArray InputToMessages(JsonNode? input, JsonNode? instructions, BridgeOptions options)
    {
        var messages = new JsonArray();
        var systemText = new StringBuilder();

        if (instructions is JsonValue instructionValue
            && instructionValue.TryGetValue<string>(out string? instructionsText)
            && !string.IsNullOrWhiteSpace(instructionsText))
        {
            systemText.Append(instructionsText.Trim());
        }

        if (input is JsonValue direct && direct.TryGetValue<string>(out string? directText))
        {
            if (!string.IsNullOrWhiteSpace(directText))
                messages.Add(new JsonObject { ["role"] = "user", ["content"] = directText });
            AddSystem(messages, systemText, options);
            return messages;
        }

        if (input is not JsonArray list)
        {
            AddSystem(messages, systemText, options);
            return messages;
        }

        foreach (var entryNode in list)
        {
            if (entryNode is JsonValue textValue && textValue.TryGetValue<string>(out string? simpleText))
            {
                if (!string.IsNullOrWhiteSpace(simpleText))
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = simpleText });
                continue;
            }
            if (entryNode is not JsonObject entry) continue;

            string type = entry["type"]?.GetValue<string>() ?? "message";
            switch (type)
            {
                case "message":
                {
                    string role = entry["role"]?.GetValue<string>() ?? "user";
                    string content = JsonOps.ExtractText(entry["content"]);
                    if (string.IsNullOrWhiteSpace(content)) continue;
                    if (role is "system" or "developer")
                    {
                        if (systemText.Length > 0) systemText.Append("\n\n");
                        systemText.Append(content.Trim());
                        continue;
                    }
                    if (role is not ("user" or "assistant")) role = "user";
                    // O opencode reenvia a call TAMBEM como texto. Mantido no
                    // historico, o modelo copia o formato narrado em vez de emitir
                    // a call nativa; a estruturada continua logo abaixo.
                    if (role == "assistant" && options.LegacyHarness && !options.HasProfile)
                        content = TextGrammar.StripNarratedCalls(content);
                    if (string.IsNullOrWhiteSpace(content)) continue;
                    messages.Add(new JsonObject { ["role"] = role, ["content"] = content });
                    break;
                }
                case "function_call":
                {
                    // O cliente reenvia a call que JÁ executou: preserva o call_id
                    // para o role="tool" abaixo casar com ele.
                    string callId = entry["call_id"]?.GetValue<string>()
                                    ?? entry["id"]?.GetValue<string>()
                                    ?? ("call_" + JsonOps.NewId());
                    var call = new JsonObject
                    {
                        ["id"] = callId,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = entry["name"]?.GetValue<string>() ?? "",
                            ["arguments"] = JsonOps.ToArguments(entry["arguments"])
                        }
                    };
                    // Chamadas consecutivas sao UM turno do assistente (N tool_calls
                    // no mesmo item). Sem o merge o template upstream ve N turnos de
                    // 1 call e o modelo perde a conta de "ja chamei isto na rodada".
                    if (messages.Count > 0 && messages[^1] is JsonObject previous
                        && previous["role"]?.GetValue<string>() == "assistant"
                        && previous["content"] is null
                        && previous["tool_calls"] is JsonArray previousCalls)
                    {
                        previousCalls.Add(call);
                    }
                    else
                    {
                        messages.Add(new JsonObject
                        {
                            ["role"] = "assistant",
                            ["content"] = null,
                            ["tool_calls"] = new JsonArray(call)
                        });
                    }
                    break;
                }
                case "function_call_output":
                case "custom_tool_call_output":
                case "shell_call_output":
                case "computer_call_output":
                case "local_shell_call_output":
                case "apply_patch_call_output":
                {
                    string callId = entry["call_id"]?.GetValue<string>() ?? entry["id"]?.GetValue<string>() ?? "";
                    string resultText = JsonOps.ExtractText(entry["output"] ?? entry["result"] ?? entry["content"]);
                    if (options.ToolOutputLimit > 0 && resultText.Length > options.ToolOutputLimit)
                        resultText = resultText[..options.ToolOutputLimit];
                    messages.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = callId, ["content"] = resultText });
                    break;
                }
                // reasoning/item_reference/calls ainda não executados: ignorados
                // de propósito (não existem no fluxo chat).
            }
        }

        AddSystem(messages, systemText, options);
        return messages;
    }

    private static void AddSystem(JsonArray messages, StringBuilder systemText, BridgeOptions options)
    {
        if (systemText.Length == 0) return;
        string text = systemText.ToString();
        if (options.LegacyHarness || options.QwenStandard)
        {
            text = TextGrammar.WithSystemGuardrail(text, options.QwenStandard);
        }
        else if (!string.IsNullOrWhiteSpace(options.SystemGuardrail)
                 && !text.Contains(options.SystemGuardrail, StringComparison.OrdinalIgnoreCase))
        {
            text = text + "\n\n" + options.SystemGuardrail;
        }
        messages.Insert(0, new JsonObject { ["role"] = "system", ["content"] = text });
    }

    /// <summary>
    /// Aplica as caudas no system consolidado. Só quando o system existe: no
    /// opencode ele vem do agent.md, na Web do system_prompt do agente; sem
    /// fallback para não duplicar quando o conteúdo vem em partes.
    /// </summary>
    private static void ApplyTails(JsonArray messages, BridgeOptions options)
    {
        if (!options.Harness) return;
        if (messages.Count == 0 || messages[0] is not JsonObject system) return;
        if (system["role"]?.GetValue<string>() != "system") return;
        if (system["content"] is not JsonValue value || !value.TryGetValue<string>(out string? text)) return;

        string merged = (text ?? "").Trim();
        if (!string.IsNullOrEmpty(options.BehaviorTail))
            merged = (merged.Length > 0 ? merged + "\n\n" : "") + options.BehaviorTail;
        if (!string.IsNullOrEmpty(options.ToolsTail))
            merged = (merged.Length > 0 ? merged + "\n\n" : "") + options.ToolsTail;
        system["content"] = merged;
    }

    /// <summary>Tools Responses -> chat. Aceita o formato chat pronto e "namespace".</summary>
    public static JsonArray? ToolsToChat(JsonNode? tools, out List<string> dropped, out List<string> defaulted)
    {
        dropped = new List<string>();
        defaulted = new List<string>();
        if (tools is not JsonArray list || list.Count == 0) return null;

        var converted = new JsonArray();
        foreach (var toolNode in list)
        {
            if (toolNode is not JsonObject entry)
            {
                dropped.Add("nao-objeto");
                continue;
            }
            string type = entry["type"]?.GetValue<string>() ?? "";

            if (type == "function" && entry["function"] is JsonObject)
            {
                converted.Add(entry.DeepClone());
                continue;
            }

            if (type == "function" && entry["name"] is not null)
            {
                converted.Add(FunctionTool(entry["name"]?.GetValue<string>(), entry["description"]?.GetValue<string>(),
                    entry["parameters"] ?? entry["inputSchema"], defaulted));
                continue;
            }

            if (type == "namespace" && entry["tools"] is JsonArray nested)
            {
                int before = converted.Count;
                foreach (var nestedNode in nested)
                {
                    if (nestedNode is not JsonObject nestedTool || nestedTool["name"] is null)
                    {
                        dropped.Add("namespace-sem-nome");
                        continue;
                    }
                    converted.Add(FunctionTool(nestedTool["name"]?.GetValue<string>(),
                        nestedTool["description"]?.GetValue<string>(),
                        nestedTool["parameters"] ?? nestedTool["inputSchema"], defaulted));
                }
                if (converted.Count - before != nested.Count) dropped.Add("namespace-parcial");
                continue;
            }

            dropped.Add($"{(type.Length == 0 ? "sem-type" : type)}:{entry["name"]?.GetValue<string>() ?? "?"}");
        }
        return converted.Count > 0 ? converted : null;
    }

    private static JsonObject FunctionTool(string? name, string? description, JsonNode? schema, List<string> defaulted)
    {
        if (schema is null) defaulted.Add(name ?? "?");
        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = description ?? "",
                ["parameters"] = JsonOps.CloneOrDefault(schema)
            }
        };
    }

    /// <summary>Nomes anunciados ao modelo (namespace desce um nível).</summary>
    public static List<string> AnnouncedTools(JsonArray? tools)
    {
        var names = new List<string>();
        if (tools is null) return names;
        foreach (var tool in tools)
        {
            if (tool is not JsonObject entry) continue;
            if (entry["type"]?.GetValue<string>() == "namespace" && entry["tools"] is JsonArray nested)
            {
                foreach (var nestedTool in nested)
                    if (nestedTool?["name"]?.GetValue<string>() is string nestedName) names.Add(nestedName);
                continue;
            }
            if (entry["function"]?["name"]?.GetValue<string>() is string chatName) names.Add(chatName);
            else if (entry["name"]?.GetValue<string>() is string flatName) names.Add(flatName);
        }
        return names;
    }

    /// <summary>
    /// Remove chaves "pattern" sem âncora ^...$ dos schemas: o parser do
    /// llama.cpp rejeita o schema inteiro (400) e a tool desaparece.
    /// </summary>
    public static int StripUnanchoredPatterns(JsonNode? node, string path, List<string> hits)
    {
        int removed = 0;
        if (node is JsonObject obj)
        {
            if (obj["pattern"] is JsonValue patternValue
                && patternValue.TryGetValue<string>(out string? pattern)
                && !string.IsNullOrEmpty(pattern)
                && !(pattern.StartsWith('^') && pattern.EndsWith('$')))
            {
                obj.Remove("pattern");
                hits.Add(path + ".pattern");
                removed++;
            }
            foreach (var pair in obj.ToList())
                removed += StripUnanchoredPatterns(pair.Value, path + "." + pair.Key, hits);
        }
        else if (node is JsonArray array)
        {
            for (int index = 0; index < array.Count; index++)
                removed += StripUnanchoredPatterns(array[index], $"{path}[{index}]", hits);
        }
        return removed;
    }

    private static bool HasStop(JsonNode stop) =>
        stop is JsonArray array ? array.Count > 0
        : stop.GetValueKind() == System.Text.Json.JsonValueKind.String
          && !string.IsNullOrEmpty(stop.GetValue<string>());
}
