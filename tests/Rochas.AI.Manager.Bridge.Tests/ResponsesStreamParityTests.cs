using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ResponsesStreamParityTests
{
    [Fact]
    public void ToolArgsEmLoopInterrompeEnaoEmiteCallParcial()
    {
        var events = new List<(string Type, JsonObject Payload)>();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions
            {
                AntiRepeat = true,
                AntiRepeatWindow = 256,
                Thinking = "events",
                CoerceNarrated = false
            },
            (type, payload) => events.Add((type, payload)),
            "m", 0.1, hadTools: true);

        translator.Start();
        string repeated = string.Concat(Enumerable.Repeat(new string('x', 1200), 3));
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
                        ["function"] = new JsonObject
                        {
                            ["name"] = "write",
                            ["arguments"] = repeated
                        }
                    })
                }
            })
        });

        Assert.True(translator.RepeatTripped);
        Assert.Equal("tool-args", translator.RepeatWhere);

        var final = translator.Finish();
        var output = final["output"]!.AsArray();

        Assert.DoesNotContain(output, node => node?["type"]?.GetValue<string>() == "function_call");
        Assert.Contains(output, node =>
            node?["type"]?.GetValue<string>() == "message"
            && node["content"]![0]!["text"]!.GetValue<string>().Contains("contencao"));
    }

    [Fact]
    public void ConteudoEmLoopInterrompeOStream()
    {
        var events = new List<(string Type, JsonObject Payload)>();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions { AntiRepeat = true, AntiRepeatWindow = 64, Thinking = "events" },
            (type, payload) => events.Add((type, payload)),
            "m", 0.1);

        translator.Start();
        string block = "o mesmo raciocinio repetido varias vezes. ";
        for (int i = 0; i < 6; i++)
        {
            translator.Feed(new JsonObject
            {
                ["choices"] = new JsonArray(new JsonObject
                {
                    ["delta"] = new JsonObject { ["content"] = block }
                })
            });
            if (translator.RepeatTripped) break;
        }

        Assert.True(translator.RepeatTripped);
        Assert.Equal("content", translator.RepeatWhere);
    }

    [Fact]
    public void RepeticaoSemanticaNaoDisparaPorPadrao()
    {
        var translator = NewSemanticTranslator(enabled: false);

        translator.Start();
        FeedContent(translator, SemanticParaphrase());

        Assert.False(translator.RepeatTripped);
    }

    [Fact]
    public void RepeticaoSemanticaDisparaQuandoHabilitada()
    {
        var translator = NewSemanticTranslator(enabled: true);

        translator.Start();
        FeedContent(translator, SemanticParaphrase());

        Assert.True(translator.RepeatTripped);
        Assert.True(translator.RepeatSemantic);
        Assert.Equal("content", translator.RepeatWhere);
    }

    [Fact]
    public void RepeticaoSemanticaDisparaNoThinking()
    {
        var translator = NewSemanticTranslator(enabled: true);

        translator.Start();
        translator.Feed(new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["delta"] = new JsonObject { ["reasoning_content"] = SemanticParaphrase() }
            })
        });

        Assert.True(translator.RepeatTripped);
        Assert.True(translator.RepeatSemantic);
        Assert.Equal("thinking", translator.RepeatWhere);
    }

    [Fact]
    public void ProsaNormalNaoDisparaSondaSemantica()
    {
        var translator = NewSemanticTranslator(enabled: true);

        translator.Start();
        FeedContent(translator,
            "a bridge converte respostas do protocolo responses para chat completions e preserva o historico de tool calls entre turnos. " +
            "o breaker injeta aviso apos falhas consecutivas no mesmo alvo e encerra o turno no limite duro. " +
            "o contexto e compactado quando o usage passa do limiar configurado no host. " +
            "o treino qlora prepara dataset chat e exporta gguf para avaliacao local. " +
            "o pipeline de avaliacao compara metricas de acuracia e latencia antes de promover o artefato. " +
            "o manager registra evidencia e vincula o resultado ao card de workflow.");

        Assert.False(translator.RepeatTripped);
    }

    private static ResponsesStreamTranslator NewSemanticTranslator(bool enabled)
        => new(
            new BridgeOptions
            {
                AntiRepeat = true,
                AntiRepeatSemantic = enabled,
                AntiRepeatWindow = 128,
                Thinking = "events"
            },
            (_, _) => { },
            "m", 0.1);

    private static void FeedContent(ResponsesStreamTranslator translator, string text)
        => translator.Feed(new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["delta"] = new JsonObject { ["content"] = text }
            })
        });

    private static string SemanticParaphrase()
        => "precisamos revisar a arquitetura da bridge porque o host mudou e o comportamento " +
           "esperado nao corresponde aquilo que o manager executa hoje com fila processamento paralelo. " +
           "e necessario revisar a arquitetura da bridge pois o host mudou, entao o comportamento " +
           "esperado nao corresponde aquilo que o manager executa atualmente com fila processamento paralelo.";
}
