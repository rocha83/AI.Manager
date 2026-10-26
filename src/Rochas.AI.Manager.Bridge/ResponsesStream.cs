using System.Text;
using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge;

/// <summary>
/// Sintetiza o SSE do Responses a partir dos deltas do chat (um feed por chunk).
/// A ordem dos eventos importa: created -> in_progress -> [reasoning] ->
/// message -> function_call -> completed, todos com sequence_number crescente.
/// O estado é acumulado aqui para que o completed traga ids iguais aos já
/// anunciados — o cliente casa os itens pelo id.
/// </summary>
public sealed class ResponsesStreamTranslator
{
    private readonly BridgeOptions options;
    private readonly Action<string, JsonObject> emit;
    private readonly string model;
    private readonly double temperature;
    private readonly bool hadTools;
    private readonly IReadOnlyList<string>? announcedTools;
    private readonly JsonNode? historyMessages;
    private readonly ToolArgsAntiRepeatReply toolArgsProbe;

    private readonly StringBuilder thinkingText = new();
    private readonly StringBuilder contentText = new();
    private readonly SortedDictionary<int, (string Id, string Name, StringBuilder Args)> toolAccum = new();

    private string responseId = "";
    private string reasoningId = "";
    private string messageId = "";
    private bool thinkOpen;
    private bool messageOpen;
    private bool inThink;
    private string carry = "";
    private string reasoningHoldBack = "";
    private int inputTokens;
    private int outputTokens;
    private int sequence;

    public ResponsesStreamTranslator(
        BridgeOptions options,
        Action<string, JsonObject> emit,
        string model,
        double temperature,
        bool hadTools = false,
        IReadOnlyList<string>? announcedTools = null,
        JsonNode? historyMessages = null)
    {
        this.options = options;
        this.emit = emit;
        this.model = model;
        this.temperature = temperature;
        this.hadTools = hadTools;
        this.announcedTools = announcedTools;
        this.historyMessages = historyMessages;
        this.toolArgsProbe = new ToolArgsAntiRepeatReply(options.AntiRepeatWindow);
    }

    /// <summary>true quando a sonda de repetição disparou no turno corrente.</summary>
    public bool RepeatTripped { get; private set; }

    /// <summary>Onde repetiu (thinking/content) e o contexto da cauda.</summary>
    public string RepeatWhere { get; private set; } = "";
    public int RepeatAt { get; private set; } = -1;
    public string RepeatContext { get; private set; } = "";

    public string ResponseId => responseId;
    public int InputTokens => inputTokens;
    public int OutputTokens => outputTokens;

    /// <summary>Emite created + in_progress. O shell vai completo nos dois eventos.</summary>
    public void Start()
    {
        responseId = "resp_" + JsonOps.NewId();
        var shell = new JsonObject
        {
            ["id"] = responseId,
            ["object"] = "response",
            ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["status"] = "in_progress",
            ["model"] = model,
            ["output"] = new JsonArray(),
            ["parallel_tool_calls"] = true,
            ["tool_choice"] = "auto",
            ["tools"] = new JsonArray(),
            ["temperature"] = temperature,
            ["service_tier"] = "auto",
            ["usage"] = null
        };
        Emit("response.created", new JsonObject { ["response"] = shell.DeepClone() });
        Emit("response.in_progress", new JsonObject { ["response"] = shell });
    }

    /// <summary>Consome um chunk chat (choices[0].delta + usage) e emite os deltas Responses.</summary>
    public void Feed(JsonObject chunk)
    {
        if (chunk["usage"] is JsonObject usage)
        {
            inputTokens = usage["prompt_tokens"]?.GetValue<int>() ?? inputTokens;
            outputTokens = usage["completion_tokens"]?.GetValue<int>() ?? outputTokens;
        }

        var choices = chunk["choices"]?.AsArray();
        var choice = choices is { Count: > 0 } ? choices[0]?.AsObject() : null;
        if (choice is null) return;
        var delta = choice["delta"]?.AsObject() ?? choice["message"]?.AsObject();
        if (delta is null) return;

        if ((delta["reasoning_content"] ?? delta["reasoning"]) is JsonValue reasoning
            && reasoning.TryGetValue<string>(out string? reasoningChunk)
            && reasoningChunk is not null)
        {
            FeedThinking(reasoningChunk);
        }
        if (delta["content"] is JsonValue content && content.TryGetValue<string>(out string? contentChunk)
            && contentChunk is not null)
        {
            RouteContent(contentChunk);
        }
        if (delta["tool_calls"] is JsonArray calls) AccumulateToolCalls(calls);
    }

    /// <summary>Fecha o turno: descarrega hold-backs, fecha itens, emite calls e completed.</summary>
    public JsonObject Finish()
    {
        if (inThink) FeedThinking(carry); else FlushMessage(carry);
        FlushReasoningRest();

        // Args de tool em loop: a call parcial nao serve; troca por aviso.
        if (RepeatTripped && RepeatWhere == "tool-args")
        {
            toolAccum.Clear();
            if (contentText.Length == 0)
                FlushMessage("\n\n" + (options.AntiRepeatNotice ?? AntiRepeat.DefaultNotice) + "\n\n");
        }

        int messageIndex = thinkOpen ? 1 : 0;

        if (thinkOpen) CloseReasoningItem();
        if (messageOpen) CloseMessageItem(messageIndex);

        // Coerção no fecho: sem deltas de call e com tools anunciadas, extrai do
        // texto acumulado (models sem tool calling nativo).
        if (toolAccum.Count == 0 && options.CoerceNarrated && hadTools)
        {
            foreach (var (name, arguments) in ToolCallTextExtractor
                         .Extract(contentText.ToString(), announcedTools))
            {
                toolAccum[toolAccum.Count] = ("call_" + JsonOps.NewId(), name, new StringBuilder(arguments));
            }
        }

        var loopedSignatures = options.AntiRepeat
            ? AntiRepeat.LoopedToolSignatures(historyMessages, 2)
            : new HashSet<string>(StringComparer.Ordinal);

        int callIndex = (thinkOpen ? 1 : 0) + (messageOpen ? 1 : 0);
        var emittedCalls = new List<(string Id, string Name, string Arguments)>();

        foreach (var (_, accumulator) in toolAccum)
        {
            string arguments = accumulator.Args.ToString();
            string name = accumulator.Name;
            if (options.QwenStandard) (name, arguments) = QwenStandard.ToClient(name, arguments);
            if (loopedSignatures.Contains(AntiRepeat.ToolSignature(name, arguments)))
            {
                EnsureMessageOpen(ref callIndex);
                FlushMessage("\n\n" + (options.AntiRepeatNotice ?? AntiRepeat.DefaultNotice) + "\n\n");
                continue;
            }
            emittedCalls.Add((accumulator.Id, name, arguments));
            EmitToolCall(callIndex, accumulator.Id, name, arguments);
            callIndex++;
        }

        // Objeto final: reusa o montador bufferizado para usage e ids coerentes.
        var synthetic = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = thinkOpen && thinkingText.Length > 0
                        ? "<think>" + thinkingText + "</think>\n" + contentText
                        : contentText.ToString(),
                    ["tool_calls"] = new JsonArray(emittedCalls.Select(call => (JsonNode)new JsonObject
                    {
                        ["id"] = call.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.Arguments }
                    }).ToArray())
                }
            }),
            ["usage"] = new JsonObject
            {
                ["prompt_tokens"] = inputTokens,
                ["completion_tokens"] = outputTokens
            }
        };

        var final = ChatToResponses.Convert(
            synthetic, model, temperature, options,
            thinkMode: options.Thinking, hadTools: hadTools);

        final["id"] = responseId;
        foreach (var node in final["output"]!.AsArray())
        {
            if (node is not JsonObject item) continue;
            string type = item["type"]?.GetValue<string>() ?? "";
            if (type == "reasoning" && thinkOpen) item["id"] = reasoningId;
            if (type == "message" && messageOpen) item["id"] = messageId;
        }

        Emit("response.completed", new JsonObject { ["response"] = final });
        return final;
    }

    /// <summary>Zera o estado acumulado para uma nova passagem (retry de contenção).</summary>
    public void Reset()
    {
        thinkingText.Clear();
        contentText.Clear();
        toolAccum.Clear();
        carry = "";
        reasoningHoldBack = "";
        inThink = false;
        RepeatTripped = false;
        RepeatWhere = "";
        RepeatAt = -1;
        RepeatContext = "";
    }

    private void Emit(string type, JsonObject payload)
    {
        payload["type"] = type;
        payload["sequence_number"] = sequence++;
        emit(type, payload);
    }

    private void FeedThinking(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (options.Thinking != "events")
        {
            // Modo "off": raciocínio vira texto comum.
            EmitThinking(ControlTags.Strip(text));
            return;
        }
        reasoningHoldBack = ControlTags.Strip(reasoningHoldBack + text);
        int hold = ControlTags.PartialLen(reasoningHoldBack);
        string emitText = hold > 0 ? reasoningHoldBack[..^hold] : reasoningHoldBack;
        reasoningHoldBack = hold > 0 ? reasoningHoldBack[^hold..] : "";
        EmitThinking(emitText);
    }

    private void FlushReasoningRest()
    {
        if (reasoningHoldBack.Length == 0) return;
        string rest = ControlTags.Strip(reasoningHoldBack);
        reasoningHoldBack = "";
        EmitThinking(rest);
    }

    private void EmitThinking(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!thinkOpen && options.Thinking == "events")
        {
            reasoningId = "rs_" + JsonOps.NewId();
            // Formato NATIVO do llama (reasoning_text + content), não o par
            // summary_text/part: com summary o opencode renderiza como snippet.
            Emit("response.output_item.added", new JsonObject
            {
                ["output_index"] = 0,
                ["item"] = new JsonObject
                {
                    ["id"] = reasoningId,
                    ["summary"] = new JsonArray(),
                    ["type"] = "reasoning",
                    ["content"] = new JsonArray(),
                    ["encrypted_content"] = "",
                    ["status"] = "in_progress"
                }
            });
            thinkOpen = true;
        }
        if (!thinkOpen)
        {
            FlushMessage(text);
            return;
        }
        thinkingText.Append(text);
        if (CheckRepeat(thinkingText.ToString(), "thinking")) return;
        Emit("response.reasoning_text.delta", new JsonObject
        {
            ["delta"] = text,
            ["item_id"] = reasoningId
        });
    }

    private void FlushMessage(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (!messageOpen)
        {
            messageId = "msg_" + JsonOps.NewId();
            int index = thinkOpen ? 1 : 0;
            Emit("response.output_item.added", new JsonObject
            {
                ["output_index"] = index,
                ["item"] = new JsonObject
                {
                    ["id"] = messageId,
                    ["type"] = "message",
                    ["role"] = "assistant",
                    ["status"] = "in_progress",
                    ["content"] = new JsonArray(),
                    ["phase"] = null
                }
            });
            Emit("response.content_part.added", new JsonObject
            {
                ["output_index"] = index,
                ["content_index"] = 0,
                ["item_id"] = messageId,
                ["part"] = new JsonObject
                {
                    ["type"] = "output_text",
                    ["text"] = "",
                    ["annotations"] = new JsonArray()
                }
            });
            messageOpen = true;
        }
        contentText.Append(text);
        if (CheckRepeat(contentText.ToString(), "content")) return;
        Emit("response.output_text.delta", new JsonObject
        {
            ["item_id"] = messageId,
            ["output_index"] = thinkOpen ? 1 : 0,
            ["content_index"] = 0,
            ["delta"] = text,
            ["logprobs"] = new JsonArray()
        });
    }

    /// <summary>Máquina de estados do híbrido: &lt;think&gt; decide o destino de cada trecho.</summary>
    private void RouteContent(string chunk)
    {
        carry += chunk;
        while (true)
        {
            if (!inThink)
            {
                int tag = carry.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                if (tag >= 0)
                {
                    FlushMessage(carry[..tag]);
                    carry = carry[(tag + 7)..];
                    inThink = true;
                    continue;
                }
                if (options.StreamPerDelta)
                {
                    // "Digitando": emite já, segurando só o que pode ser tag partida.
                    int hold = ControlTags.PartialPrefixLen(carry, "<think>");
                    int upto = carry.Length - hold;
                    if (upto > 0)
                    {
                        FlushMessage(carry[..upto]);
                        carry = carry[upto..];
                    }
                }
                else if (carry.Length > 256)
                {
                    FlushMessage(carry[..^16]);
                    carry = carry[^16..];
                }
                return;
            }

            int end = carry.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
            if (end >= 0)
            {
                FeedThinking(carry[..end]);
                carry = carry[(end + 8)..];
                inThink = false;
                continue;
            }
            if (options.StreamPerDelta)
            {
                int hold = ControlTags.PartialPrefixLen(carry, "</think>");
                int upto = carry.Length - hold;
                if (upto > 0)
                {
                    FeedThinking(carry[..upto]);
                    carry = carry[upto..];
                }
            }
            else if (carry.Length > 256)
            {
                FeedThinking(carry[..^16]);
                carry = carry[^16..];
            }
            return;
        }
    }

    private void AccumulateToolCalls(JsonArray calls)
    {
        foreach (var callNode in calls)
        {
            if (callNode is not JsonObject callObj) continue;
            int index = callObj["index"]?.GetValue<int>() ?? 0;
            if (!toolAccum.TryGetValue(index, out var accumulator))
            {
                accumulator = (callObj["id"]?.GetValue<string>() ?? ("call_" + JsonOps.NewId()), "", new StringBuilder());
                toolAccum[index] = accumulator;
            }
            var function = callObj["function"]?.AsObject();
            if (function?["name"]?.GetValue<string>() is string name && name.Length > 0)
                toolAccum[index] = (accumulator.Id, name, accumulator.Args);
            if (function?["arguments"]?.GetValue<string>() is string arguments)
            {
                accumulator.Args.Append(arguments);
                if (options.AntiRepeat && !RepeatTripped
                    && accumulator.Args.Length >= ToolArgsAntiRepeatReply.ProbeMinChars
                    && toolArgsProbe.IsRepeat(accumulator.Args.ToString()))
                {
                    RepeatTripped = true;
                    RepeatWhere = "tool-args";
                    string normalized = JsonOps.Normalize(accumulator.Args.ToString());
                    RepeatAt = Math.Max(0, normalized.Length - options.AntiRepeatWindow);
                    RepeatContext = normalized.Length > RepeatAt
                        ? normalized[RepeatAt..Math.Min(normalized.Length, RepeatAt + 40)]
                        : "";
                }
            }
        }
    }

    private void EnsureMessageOpen(ref int callIndex)
    {
        if (messageOpen) return;
        messageId = "msg_" + JsonOps.NewId();
        Emit("response.output_item.added", new JsonObject
        {
            ["output_index"] = callIndex,
            ["item"] = new JsonObject
            {
                ["id"] = messageId,
                ["type"] = "message",
                ["role"] = "assistant",
                ["status"] = "in_progress",
                ["content"] = new JsonArray()
            }
        });
        Emit("response.content_part.added", new JsonObject
        {
            ["output_index"] = callIndex,
            ["content_index"] = 0,
            ["item_id"] = messageId,
            ["part"] = new JsonObject { ["type"] = "output_text", ["text"] = "", ["annotations"] = new JsonArray() }
        });
        messageOpen = true;
        callIndex++;
    }

    private void CloseReasoningItem()
    {
        string thinkingFinal = ControlTags.Strip(thinkingText.ToString());
        Emit("response.output_item.done", new JsonObject
        {
            ["output_index"] = 0,
            ["item"] = new JsonObject
            {
                ["id"] = reasoningId,
                ["summary"] = new JsonArray(),
                ["type"] = "reasoning",
                ["content"] = new JsonArray(new JsonObject { ["text"] = thinkingFinal }),
                ["encrypted_content"] = "",
                ["status"] = "completed"
            }
        });
    }

    private void CloseMessageItem(int index)
    {
        string fullText = ChatToResponses.RemoveTemplateMarkers(contentText.ToString());
        Emit("response.output_text.done", new JsonObject
        {
            ["item_id"] = messageId,
            ["output_index"] = index,
            ["content_index"] = 0,
            ["text"] = fullText,
            ["logprobs"] = new JsonArray()
        });
        Emit("response.content_part.done", new JsonObject
        {
            ["output_index"] = index,
            ["content_index"] = 0,
            ["item_id"] = messageId,
            ["part"] = new JsonObject
            {
                ["type"] = "output_text",
                ["text"] = fullText,
                ["annotations"] = new JsonArray()
            }
        });
        Emit("response.output_item.done", new JsonObject
        {
            ["output_index"] = index,
            ["item"] = new JsonObject
            {
                ["id"] = messageId,
                ["type"] = "message",
                ["role"] = "assistant",
                ["status"] = "completed",
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "output_text",
                    ["text"] = fullText,
                    ["annotations"] = new JsonArray(),
                    ["logprobs"] = null
                })
            }
        });
    }

    private void EmitToolCall(int index, string id, string name, string arguments)
    {
        var item = new JsonObject
        {
            ["type"] = "function_call",
            ["id"] = id,
            ["call_id"] = id,
            ["name"] = name,
            ["arguments"] = arguments
        };
        Emit("response.output_item.added", new JsonObject
        {
            ["output_index"] = index,
            ["item"] = (JsonNode)item.DeepClone()
        });
        Emit("response.function_call_arguments.delta", new JsonObject
        {
            ["item_id"] = id,
            ["output_index"] = index,
            ["delta"] = arguments
        });
        Emit("response.function_call_arguments.done", new JsonObject
        {
            ["item_id"] = id,
            ["output_index"] = index,
            ["arguments"] = arguments
        });
        Emit("response.output_item.done", new JsonObject { ["output_index"] = index, ["item"] = item });
    }

    /// <summary>Compara a cauda com o histórico e registra onde repetiu.</summary>
    private bool CheckRepeat(string accumulated, string where)
    {
        if (!options.AntiRepeat || RepeatTripped) return RepeatTripped;
        int at = AntiRepeat.DuplicateAt(accumulated, options.AntiRepeatWindow);
        if (at < 0) return false;
        RepeatTripped = true;
        RepeatWhere = where;
        RepeatAt = at;
        string normalized = JsonOps.Normalize(accumulated);
        RepeatContext = normalized.Length > at
            ? normalized[at..Math.Min(normalized.Length, at + 40)]
            : "";
        return true;
    }
}

/// <summary>Síntese de SSE a partir de um objeto Responses completo (upstream sem stream).</summary>
public static class ResponsesSse
{
    public static string Build(JsonObject response, string? thinkMode = null)
    {
        var builder = new StringBuilder();
        int sequence = 0;
        void Write(string type, JsonObject payload)
        {
            payload["type"] = type;
            payload["sequence_number"] = sequence++;
            builder.Append("event: ").Append(type).Append('\n')
                   .Append("data: ").Append(payload.ToJsonString(JsonOps.SerializerOptions)).Append("\n\n");
        }

        Write("response.created", new JsonObject { ["response"] = response.DeepClone() });
        Write("response.in_progress", new JsonObject { ["response"] = response.DeepClone() });

        int outputIndex = 0;
        foreach (var node in response["output"]!.AsArray())
        {
            if (node is not JsonObject original) continue;
            var item = (JsonObject)original.DeepClone();
            string type = item["type"]?.GetValue<string>() ?? "";

            if (type == "reasoning")
            {
                string thinking = item["content"]?[0]?["text"]?.GetValue<string>()
                                  ?? item["summary"]?[0]?["text"]?.GetValue<string>() ?? "";
                string reasoningId = item["id"]?.GetValue<string>() ?? ("rs_" + JsonOps.NewId());
                Write("response.output_item.added", new JsonObject
                {
                    ["output_index"] = outputIndex,
                    ["item"] = new JsonObject
                    {
                        ["id"] = reasoningId,
                        ["summary"] = new JsonArray(),
                        ["type"] = "reasoning",
                        ["content"] = new JsonArray(),
                        ["encrypted_content"] = "",
                        ["status"] = "in_progress"
                    }
                });
                if (thinking.Length > 0)
                    Write("response.reasoning_text.delta", new JsonObject
                    {
                        ["delta"] = thinking,
                        ["item_id"] = reasoningId
                    });
                Write("response.output_item.done", new JsonObject
                {
                    ["output_index"] = outputIndex,
                    ["item"] = new JsonObject
                    {
                        ["id"] = reasoningId,
                        ["summary"] = new JsonArray(),
                        ["type"] = "reasoning",
                        ["content"] = new JsonArray(new JsonObject { ["text"] = thinking }),
                        ["encrypted_content"] = "",
                        ["status"] = "completed"
                    }
                });
                outputIndex++;
                continue;
            }

            if (type == "message")
            {
                string text = item["content"]?[0]?["text"]?.GetValue<string>() ?? "";
                string messageId = item["id"]?.GetValue<string>() ?? ("msg_" + JsonOps.NewId());
                Write("response.output_item.added", new JsonObject
                {
                    ["output_index"] = outputIndex,
                    ["item"] = new JsonObject
                    {
                        ["id"] = messageId,
                        ["type"] = "message",
                        ["role"] = "assistant",
                        ["status"] = "in_progress",
                        ["content"] = new JsonArray()
                    }
                });
                Write("response.content_part.added", new JsonObject
                {
                    ["output_index"] = outputIndex,
                    ["content_index"] = 0,
                    ["item_id"] = messageId,
                    ["part"] = new JsonObject { ["type"] = "output_text", ["text"] = "", ["annotations"] = new JsonArray() }
                });
                if (text.Length > 0)
                    Write("response.output_text.delta", new JsonObject
                    {
                        ["item_id"] = messageId,
                        ["output_index"] = outputIndex,
                        ["content_index"] = 0,
                        ["delta"] = text,
                        ["logprobs"] = new JsonArray()
                    });
                Write("response.output_text.done", new JsonObject
                {
                    ["item_id"] = messageId,
                    ["output_index"] = outputIndex,
                    ["content_index"] = 0,
                    ["text"] = text,
                    ["logprobs"] = new JsonArray()
                });
                Write("response.content_part.done", new JsonObject
                {
                    ["output_index"] = outputIndex,
                    ["content_index"] = 0,
                    ["item_id"] = messageId,
                    ["part"] = new JsonObject { ["type"] = "output_text", ["text"] = text, ["annotations"] = new JsonArray() }
                });
                Write("response.output_item.done", new JsonObject { ["output_index"] = outputIndex, ["item"] = item });
                outputIndex++;
                continue;
            }

            if (type == "function_call")
            {
                string id = item["id"]?.GetValue<string>() ?? ("call_" + JsonOps.NewId());
                string name = item["name"]?.GetValue<string>() ?? "";
                string arguments = item["arguments"]?.GetValue<string>() ?? "{}";
                Write("response.output_item.added", new JsonObject { ["output_index"] = outputIndex, ["item"] = (JsonNode)item.DeepClone() });
                Write("response.function_call_arguments.delta", new JsonObject
                {
                    ["item_id"] = id,
                    ["output_index"] = outputIndex,
                    ["delta"] = arguments
                });
                Write("response.function_call_arguments.done", new JsonObject
                {
                    ["item_id"] = id,
                    ["output_index"] = outputIndex,
                    ["arguments"] = arguments
                });
                Write("response.output_item.done", new JsonObject { ["output_index"] = outputIndex, ["item"] = item });
                outputIndex++;
            }
        }

        Write("response.completed", new JsonObject { ["response"] = response });
        builder.Append("data: [DONE]\n\n");
        return builder.ToString();
    }
}
