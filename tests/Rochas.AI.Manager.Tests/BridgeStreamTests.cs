using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge;
using Rochas.AI.Manager.Domain.Harness;
using Xunit;

namespace Rochas.AI.Manager.Tests;

public class ResponsesStreamTranslatorTests
{
    private sealed class Recorder
    {
        public List<(string Type, JsonObject Payload)> Events { get; } = new();
        public void Emit(string type, JsonObject payload) => Events.Add((type, payload));
        public int Count(string type) => Events.Count(e => e.Type == type);
        public string TextDeltas(string eventType) => string.Concat(Events
            .Where(e => e.Type == eventType)
            .Select(e => e.Payload["delta"]?.GetValue<string>() ?? ""));
    }

    private static JsonObject ContentChunk(string text) => new()
    {
        ["choices"] = new JsonArray(new JsonObject
        {
            ["delta"] = new JsonObject { ["content"] = text }
        })
    };

    private static JsonObject ReasoningChunk(string text) => new()
    {
        ["choices"] = new JsonArray(new JsonObject
        {
            ["delta"] = new JsonObject { ["reasoning_content"] = text }
        })
    };

    [Fact]
    public void EmiteSequenciaNativaDeEventos()
    {
        var recorder = new Recorder();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions { Thinking = "events" }, recorder.Emit, "m", 0.5);

        translator.Start();
        translator.Feed(ReasoningChunk("pensando"));
        translator.Feed(ContentChunk("ola"));
        var final = translator.Finish();

        var types = recorder.Events.Select(e => e.Type).ToList();
        Assert.Equal("response.created", types[0]);
        Assert.Equal("response.in_progress", types[1]);
        Assert.Equal("response.output_item.added", types[2]);
        Assert.Contains("response.reasoning_text.delta", types);
        Assert.DoesNotContain("response.reasoning_summary_text.delta", types);
        Assert.Contains("response.content_part.added", types);
        Assert.Contains("response.output_text.delta", types);
        Assert.Equal("response.completed", types[^1]);
        Assert.Equal(translator.ResponseId, final["id"]!.GetValue<string>());

        // item reasoning: shape nativo (content com o texto, summary vazio)
        var reasoningDone = recorder.Events.First(e =>
            e.Type == "response.output_item.done"
            && e.Payload["item"]!["type"]!.GetValue<string>() == "reasoning");
        Assert.Equal("pensando", reasoningDone.Payload["item"]!["content"]![0]!["text"]!.GetValue<string>());
        Assert.Empty(reasoningDone.Payload["item"]!["summary"]!.AsArray());

        // sequence_number sempre crescente
        int previous = -1;
        foreach (var (_, payload) in recorder.Events)
        {
            int sequence = payload["sequence_number"]!.GetValue<int>();
            Assert.True(sequence > previous);
            previous = sequence;
        }
    }

    [Fact]
    public void HoldBackImpedeTagPartidaVazar()
    {
        var recorder = new Recorder();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions { Thinking = "events" }, recorder.Emit, "m", 0.5);

        translator.Start();
        translator.Feed(ReasoningChunk("A</thou"));
        translator.Feed(ReasoningChunk("ght>B"));
        translator.Finish();

        string deltas = recorder.TextDeltas("response.reasoning_text.delta");
        Assert.Equal("AB", deltas);
        Assert.DoesNotContain("</thought>", deltas);
    }

    [Fact]
    public void StreamPerDeltaControlaGranularidadeDoContent()
    {
        var perDelta = new Recorder();
        var a = new ResponsesStreamTranslator(
            new BridgeOptions { StreamPerDelta = true }, perDelta.Emit, "m", 0.5);
        a.Start();
        a.Feed(ContentChunk("bloco1 "));
        a.Feed(ContentChunk("bloco2 "));
        a.Finish();

        var batched = new Recorder();
        var b = new ResponsesStreamTranslator(
            new BridgeOptions { StreamPerDelta = false }, batched.Emit, "m", 0.5);
        b.Start();
        b.Feed(ContentChunk("bloco1 "));
        b.Feed(ContentChunk("bloco2 "));
        b.Finish();

        Assert.Equal(2, perDelta.Count("response.output_text.delta"));
        Assert.Equal(1, batched.Count("response.output_text.delta"));
        Assert.Equal("bloco1 bloco2 ", perDelta.TextDeltas("response.output_text.delta"));
        Assert.Equal("bloco1 bloco2 ", batched.TextDeltas("response.output_text.delta"));
    }

    [Fact]
    public void EmiteFunctionCallAcumuladaNoFecho()
    {
        var recorder = new Recorder();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions { Thinking = "off" }, recorder.Emit, "m", 0.5, hadTools: true, announcedTools: new[] { "shell" });

        translator.Start();
        translator.Feed(new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["delta"] = new JsonObject
                {
                    ["tool_calls"] = new JsonArray(new JsonObject
                    {
                        ["index"] = 0,
                        ["id"] = "call_1",
                        ["function"] = new JsonObject { ["name"] = "shell", ["arguments"] = "{\"comm" }
                    })
                }
            })
        });
        translator.Feed(new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["delta"] = new JsonObject
                {
                    ["tool_calls"] = new JsonArray(new JsonObject
                    {
                        ["index"] = 0,
                        ["function"] = new JsonObject { ["arguments"] = "and\":\"ls\"}" }
                    })
                }
            })
        });
        var final = translator.Finish();

        var call = final["output"]!.AsArray()
            .First(node => node!["type"]!.GetValue<string>() == "function_call")!;
        Assert.Equal("shell", call["name"]!.GetValue<string>());
        Assert.Equal("{\"command\":\"ls\"}", call["arguments"]!.GetValue<string>());
        Assert.Equal(1, recorder.Count("response.function_call_arguments.done"));
        Assert.Equal("{\"command\":\"ls\"}",
            recorder.Events.First(e => e.Type == "response.function_call_arguments.done")
                .Payload["arguments"]!.GetValue<string>());
    }

    [Fact]
    public void AntiRepeatDisparaComTextoRepetido()
    {
        var recorder = new Recorder();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions { Thinking = "events", AntiRepeatWindow = 64 }, recorder.Emit, "m", 0.5);

        string bloco = string.Concat(Enumerable.Repeat("A raposa pula cerca. ", 6));
        translator.Start();
        translator.Feed(ReasoningChunk(bloco));
        translator.Feed(ReasoningChunk(bloco));

        Assert.True(translator.RepeatTripped);
        Assert.Equal("thinking", translator.RepeatWhere);
        Assert.True(translator.RepeatAt >= 0);
    }
}

public class BridgeOptionsFactoryTests
{
    [Fact]
    public void DadoVenceOsDefaults()
    {
        var content = new HarnessContent(
            Core: null, ToolCatalog: null, Nucleo: null,
            Behavior: "BEHAVIOR-DB", ToolsUsage: "TOOLS-DB");

        var options = BridgeOptionsFactory.FromContent(content);

        Assert.Equal("BEHAVIOR-DB", options.BehaviorTail);
        Assert.Equal("TOOLS-DB", options.ToolsTail);
        // anti-repeat e logica (config), nao enunciado do banco
        Assert.Null(options.AntiRepeatNotice);
    }

    [Fact]
    public void SemDadoMantemOsDefaultsDoMotor()
    {
        var options = BridgeOptionsFactory.FromContent(HarnessContent.Empty);

        Assert.Equal(HarnessTails.Behavior, options.BehaviorTail);
        Assert.Equal(HarnessTails.Tools, options.ToolsTail);
        Assert.Null(options.AntiRepeatNotice);
    }

    [Fact]
    public void CoercaoVemDoCadastroDoModelo()
    {
        var model = new ModelBinding
        {
            Thinking = "off", Temperature = 0.3, MaxTokens = 16000,
            ContextWindow = 65536, UsesToolCoercion = true
        };
        var content = new HarnessContent(null, null, null, "B", "T");

        var options = BridgeOptionsFactory.FromModel(model, content);

        Assert.True(options.CoerceNarrated);
        Assert.Equal("off", options.Thinking);
        Assert.Equal(0.3, options.Temperature);
        Assert.Equal(16000, options.MaxTokens);
        Assert.Equal(65536, options.ContextTokens);
        Assert.Equal("B", options.BehaviorTail);
        Assert.Equal("T", options.ToolsTail);
    }

    [Fact]
    public void ModeloComToolCallingNativoNaoCoage()
    {
        var options = BridgeOptionsFactory.FromModel(new ModelBinding { UsesToolCoercion = false });
        Assert.False(options.CoerceNarrated);
    }

    [Fact]
    public void BaselineEhPreservado()
    {
        var baseline = new BridgeOptions { Thinking = "off", StreamPerDelta = false };
        var options = BridgeOptionsFactory.FromContent(
            new HarnessContent(null, null, null, "B", null), baseline);

        Assert.Equal("off", options.Thinking);
        Assert.False(options.StreamPerDelta);
        Assert.Equal("B", options.BehaviorTail);
    }
}
