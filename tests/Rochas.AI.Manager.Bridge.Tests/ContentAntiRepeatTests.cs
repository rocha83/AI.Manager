using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ContentAntiRepeatTests
{
    [Fact]
    public void DetectaCaudaVerbatim()
    {
        string text = string.Concat(Enumerable.Repeat("o mesmo raciocinio repetido ", 40));
        var detector = new AntiRepeatReply(64);

        Assert.True(detector.IsRepeat(text));
        Assert.True(detector.IsRepeat(text));
        Assert.True(ContentAntiRepeat.TailDuplicateAt(text, 64) >= 0);
    }

    [Fact]
    public void DetectaVariacaoCurtaComLevenshtein()
    {
        string text = string.Concat(Enumerable.Repeat("Ola! tudo bem? ", 20));
        var detector = new StreamAntiRepeatReply(32);

        Assert.True(detector.IsRepeat(text));
    }

    [Fact]
    public void DetectaRepeticaoSemanticaComPalavrasDiferentes()
    {
        string first =
            "precisamos revisar a arquitetura da bridge porque o host mudou e o comportamento " +
            "esperado nao corresponde aquilo que o manager executa hoje com fila processamento paralelo. ";
        string paraphrase =
            "e necessario revisar a arquitetura da bridge pois o host mudou, entao o comportamento " +
            "esperado nao corresponde aquilo que o manager executa atualmente com fila processamento paralelo.";
        string text = first + paraphrase;

        int at = AntiRepeat.SemanticDuplicateAt(text, 128);

        Assert.True(at >= 0);
        Assert.False(new AntiRepeatReply(128).IsRepeat(text));
    }

    [Fact]
    public void NaoDetectaTextoNovo()
    {
        string text = "primeira frase completamente diferente da segunda, " +
                      "e a terceira tambem muda o assunto para outra coisa.";
        Assert.False(new AntiRepeatReply(64).IsRepeat(text));
    }

    [Fact]
    public void DetectaArgsDeToolEmLoop()
    {
        string content = new string('x', 1200) + new string('x', 1200) + new string('x', 1200);
        var args = new JsonObject { ["content"] = content }.ToJsonString();
        var probe = new ToolArgsAntiRepeatReply(64);

        Assert.True(probe.IsRepeat(args));
    }

    [Fact]
    public void SignatureCanonicaIgnoraEspacoEApelido()
    {
        Assert.Equal(
            ToolsAntiRepeat.Signature("read", """{"path":"a.txt"}"""),
            ToolsAntiRepeat.Signature("read", """{"path": "a.txt"}"""));
        Assert.Equal(
            ToolsAntiRepeat.Signature("bash", """{"command":"ls"}"""),
            ToolsAntiRepeat.Signature("shell", """{"command":"ls"}"""));
    }

    [Fact]
    public void BreakerNudgeNoMesmoAlvo()
    {
        var messages = FailureHistory(3, "proj-teste-00");
        var breaker = new ToolsBreaker(3, 6);

        var verdict = breaker.Evaluate(messages);

        Assert.Equal(BreakerVerdict.Nudge, verdict.Verdict);
        Assert.Equal(3, verdict.Streak);
        Assert.Equal(1, verdict.DistinctTargets);
        Assert.Contains("BRIDGE-BREAKER", verdict.Notice);
        Assert.True(breaker.ApplyNudge(messages, verdict.Notice));
        Assert.Contains("BRIDGE-BREAKER",
            messages[^1]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void BreakerTripAposSeisFalhas()
    {
        var verdict = new ToolsBreaker(3, 6).Evaluate(FailureHistory(6, "proj-teste-00"));

        Assert.Equal(BreakerVerdict.Trip, verdict.Verdict);
        Assert.Equal(6, verdict.Streak);
        Assert.Contains("encerrou este turno", verdict.Notice);
    }

    [Fact]
    public void BreakerNaoDisparaComAlvosDiferentes()
    {
        var messages = FailureHistory(3, "proj-");
        messages[0]!["tool_calls"]![0]!["function"]!["arguments"] = """{"path":"/alpha.txt"}""";
        messages[2]!["tool_calls"]![0]!["function"]!["arguments"] = """{"path":"/beta.txt"}""";
        messages[4]!["tool_calls"]![0]!["function"]!["arguments"] = """{"path":"/gamma.txt"}""";

        var verdict = new ToolsBreaker(3, 6).Evaluate(messages);

        Assert.Equal(BreakerVerdict.None, verdict.Verdict);
        Assert.Equal(3, verdict.DistinctTargets);
    }

    [Fact]
    public void RepeatedCallContaAssinaturas()
    {
        var history = new JsonArray();
        for (int i = 0; i < 2; i++)
        {
            history.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["tool_calls"] = new JsonArray(new JsonObject
                {
                    ["id"] = "call_" + i,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = "read",
                        ["arguments"] = """{"path":"a.txt"}"""
                    }
                })
            });
        }

        var looped = new RepeatedCall(2).Find(history);

        Assert.Contains(ToolsAntiRepeat.Signature("read", """{"path":"a.txt"}"""), looped);
    }

    private static JsonArray FailureHistory(int failures, string pathPrefix)
    {
        var messages = new JsonArray();
        for (int i = 0; i < failures; i++)
        {
            string id = "call_" + i;
            messages.Add(new JsonObject
            {
                ["role"] = "assistant",
                ["content"] = null,
                ["tool_calls"] = new JsonArray(new JsonObject
                {
                    ["id"] = id,
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = "read",
                        ["arguments"] = new JsonObject
                        {
                            ["path"] = pathPrefix + (i + 1).ToString("000") + ".txt"
                        }.ToJsonString()
                    }
                })
            });
            messages.Add(new JsonObject
            {
                ["role"] = "tool",
                ["tool_call_id"] = id,
                ["content"] = "No such file or directory"
            });
        }
        return messages;
    }
}
