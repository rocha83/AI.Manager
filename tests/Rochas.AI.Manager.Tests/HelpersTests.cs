using System.Text.Json;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Application.Helpers;
using Xunit;

namespace Rochas.AI.Manager.Tests;

public class ToolCallCoercerTests
{
    [Fact]
    public void ExtractAngleCall()
    {
        var content = "<{\"name\":\"foo\",\"arguments\":{\"x\":1}}>";
        var calls = ToolCallCoercer.ExtractTextToolCalls(content);
        Assert.Single(calls);
        Assert.Equal("foo", calls[0].Name);
    }

    [Fact]
    public void ExtractAssistantToolCall()
    {
        var content = "[Assistant tool call]: bar({\"y\":2})";
        var calls = ToolCallCoercer.ExtractTextToolCalls(content);
        Assert.Single(calls);
        Assert.Equal("bar", calls[0].Name);
    }

    [Fact]
    public void CoerceChatCalls_SetsToolCalls()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"<\\\"name\\\":\\\"baz\\\",\\\"arguments\\\":{}}>\",\"role\":\"assistant\"},\"finish_reason\":\"stop\"}]}";
        // build proper
        var root = new JsonObject
        {
            ["choices"] = new JsonArray
            {
                new JsonObject
                {
                    ["message"] = new JsonObject { ["content"] = "<{\"name\":\"baz\",\"arguments\":{}}>", ["role"] = "assistant" },
                    ["finish_reason"] = "stop"
                }
            }
        };
        var n = ToolCallCoercer.CoerceChatCalls(root);
        Assert.Equal(1, n);
        Assert.NotNull(root["choices"]![0]!["message"]!["tool_calls"]);
    }

    [Fact]
    public void EmptyContent_ReturnsEmpty()
    {
        var calls = ToolCallCoercer.ExtractTextToolCalls("");
        Assert.Empty(calls);
    }

    [Fact]
    public void KnownFilter()
    {
        var content = "<{\"name\":\"foo\",\"arguments\":{}}>";
        var calls = ToolCallCoercer.ExtractTextToolCalls(content, new List<string> { "bar" });
        Assert.Empty(calls);
    }
}

public class ReasoningSanitizerTests
{
    [Fact]
    public void RemovesReasoningContent()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"hi\",\"reasoning_content\":\"think\"}}]}";
        var out_ = ReasoningSanitizer.Sanitize(json);
        Assert.DoesNotContain("reasoning_content", out_);
        Assert.Contains("hi", out_);
    }

    [Fact]
    public void InvalidJson_ReturnsOriginal()
    {
        var text = "not json";
        Assert.Equal(text, ReasoningSanitizer.Sanitize(text));
    }
}
public class ReasoningControlTagTests
{
    [Fact]
    public void StripControlTags_RemoveTagsDeControle()
    {
        Assert.Equal("pensando", ReasoningSanitizer.StripControlTags("<|channel>pensando<thought></thought>"));
    }

    [Fact]
    public void SanitizeResponses_LimpaSoReasoningEPreservaShapeNativo()
    {
        var json = "{\"output\":[" +
                   "{\"type\":\"reasoning\",\"summary\":[],\"content\":[{\"text\":\"<|channel>raciocinio</thought>\"}]}," +
                   "{\"type\":\"message\",\"content\":[{\"type\":\"output_text\",\"text\":\"<thought>texto do usuario</thought>\"}]}]}";

        var root = JsonNode.Parse(ReasoningSanitizer.SanitizeResponses(json))!.AsObject();

        var reasoning = root["output"]![0]!.AsObject();
        Assert.Equal("raciocinio", reasoning["content"]![0]!["text"]!.GetValue<string>());
        Assert.Empty(reasoning["summary"]!.AsArray());
        // Mensagem do usuario/assistente nao e tocada: so o reasoning e limpo.
        Assert.Equal("<thought>texto do usuario</thought>",
            root["output"]![1]!["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void SanitizeResponses_JsonInvalido_DevolveEntrada()
    {
        Assert.Equal("nao-json", ReasoningSanitizer.SanitizeResponses("nao-json"));
    }
}

public class ToolListNormalizerTests
{
    [Fact]
    public void TrocaTaskPorSubagent_PreservaExtras()
    {
        Assert.Equal("read,write,subagent,shell",
            ToolListNormalizer.Normalize("read,write,task,shell", "read,write,shell,subagent", delegating: true));
    }

    [Fact]
    public void AdicionaSubagentQuandoRoleDelega()
    {
        Assert.Equal("read,grep,subagent",
            ToolListNormalizer.Normalize("read,grep", "read,grep", delegating: true));
    }

    [Fact]
    public void RemoveSubagentQuandoRoleNaoDelega()
    {
        Assert.Equal("read,shell",
            ToolListNormalizer.Normalize("read,subagent,shell", "read,shell,execute", delegating: false));
    }

    [Fact]
    public void CsvVazioUsaSeed()
    {
        Assert.Equal("read,glob,subagent",
            ToolListNormalizer.Normalize("", "read,glob,subagent", delegating: true));
    }
}
