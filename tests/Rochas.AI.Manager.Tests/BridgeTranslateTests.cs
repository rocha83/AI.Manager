using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge;
using Rochas.AI.Manager.Domain.Harness;
using Xunit;

namespace Rochas.AI.Manager.Tests;

public class ResponsesToChatTests
{
    private static JsonObject Request(string inputJson) => new()
    {
        ["model"] = "openai/gemma-4-12b",
        ["instructions"] = "Voce e um agente.",
        ["input"] = JsonNode.Parse(inputJson)
    };

    [Fact]
    public void ConverteConversaCompletaComToolCall()
    {
        var request = Request("""
        [
          {"type":"message","role":"user","content":[{"type":"input_text","text":"rode ls"}]},
          {"type":"function_call","call_id":"call_1","name":"shell","arguments":"{\"command\":\"ls\"}"},
          {"type":"function_call_output","call_id":"call_1","output":"a.txt"}
        ]
        """);

        var result = ResponsesToChat.Convert(request, new BridgeOptions { Harness = false }, "gemma-4-12b");
        var messages = result.Body["messages"]!.AsArray();

        Assert.Equal(4, messages.Count); // system + user + assistant(tool_call) + tool
        Assert.Equal("system", messages[0]!["role"]!.GetValue<string>());
        Assert.Equal("user", messages[1]!["role"]!.GetValue<string>());
        Assert.Equal("assistant", messages[2]!["role"]!.GetValue<string>());
        Assert.Equal("call_1", messages[2]!["tool_calls"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("tool", messages[3]!["role"]!.GetValue<string>());
        Assert.Equal("a.txt", messages[3]!["content"]!.GetValue<string>());
        // o id pedido pelo cliente e ecoado; o servido e o upstream
        Assert.Equal("openai/gemma-4-12b", result.RequestedModel);
        Assert.Equal("gemma-4-12b", result.Body["model"]!.GetValue<string>());
    }

    [Fact]
    public void AnexaAsCaudasNoSystemConsolidado()
    {
        var request = Request("""[{"type":"message","role":"user","content":"oi"}]""");
        var options = new BridgeOptions { BehaviorTail = "BEHAVIOR", ToolsTail = "TOOLS" };

        var result = ResponsesToChat.Convert(request, options, "m");
        string system = result.Body["messages"]![0]!["content"]!.GetValue<string>();

        Assert.Contains("Voce e um agente.", system);
        Assert.Contains("BEHAVIOR", system);
        Assert.Contains("TOOLS", system);
        Assert.True(system.IndexOf("BEHAVIOR", StringComparison.Ordinal) < system.IndexOf("TOOLS", StringComparison.Ordinal));
    }

    [Fact]
    public void NaoAnexaCaudaSemSystem()
    {
        var request = new JsonObject
        {
            ["model"] = "m",
            ["input"] = JsonNode.Parse("""[{"type":"message","role":"user","content":"oi"}]""")
        };
        var result = ResponsesToChat.Convert(request, new BridgeOptions { BehaviorTail = "BEHAVIOR" }, "m");

        Assert.Single(result.Body["messages"]!.AsArray());
        Assert.Equal("user", result.Body["messages"]![0]!["role"]!.GetValue<string>());
    }

    [Fact]
    public void ConverteToolsFlatENamespaceParaChat()
    {
        var request = new JsonObject
        {
            ["model"] = "m",
            ["input"] = new JsonArray(),
            ["tools"] = JsonNode.Parse("""
            [
              {"type":"function","name":"read","description":"le","inputSchema":{"type":"object"}},
              {"type":"namespace","name":"edit","tools":[{"name":"apply","description":"aplica","inputSchema":{"type":"object"}}]},
              {"type":"web_search"}
            ]
            """)
        };

        var result = ResponsesToChat.Convert(request, new BridgeOptions { Harness = false }, "m");
        var tools = result.Body["tools"]!.AsArray();

        Assert.Equal(2, tools.Count);
        Assert.Equal("read", tools[0]!["function"]!["name"]!.GetValue<string>());
        Assert.Equal("apply", tools[1]!["function"]!["name"]!.GetValue<string>());
        Assert.Contains(result.DroppedTools, item => item.StartsWith("web_search"));
    }

    [Fact]
    public void RemovePatternSemAncoraEMantemAncorado()
    {
        var request = new JsonObject
        {
            ["model"] = "m",
            ["input"] = new JsonArray(),
            ["tools"] = JsonNode.Parse("""
            [
              {"type":"function","name":"a","parameters":{"type":"object","properties":{
                 "x":{"type":"string","pattern":"[a-z]+"},
                 "y":{"type":"string","pattern":"^[0-9]+$"}}}}
            ]
            """)
        };

        var result = ResponsesToChat.Convert(request, new BridgeOptions { Harness = false }, "m");
        var properties = result.Body["tools"]![0]!["function"]!["parameters"]!["properties"]!;

        Assert.Null(properties["x"]!["pattern"]);
        Assert.Equal("^[0-9]+$", properties["y"]!["pattern"]!.GetValue<string>());
        Assert.Single(result.PatternHits);
    }
}

public class ChatToResponsesTests
{
    private static JsonObject Chat(string content, string? reasoning = null, JsonArray? calls = null)
    {
        var message = new JsonObject { ["role"] = "assistant", ["content"] = content };
        if (reasoning is not null) message["reasoning_content"] = reasoning;
        if (calls is not null) message["tool_calls"] = calls;
        return new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject { ["message"] = message }),
            ["usage"] = new JsonObject { ["prompt_tokens"] = 10, ["completion_tokens"] = 20 }
        };
    }

    [Fact]
    public void ThinkingViraItemReasoningComShapeNativo()
    {
        var response = ChatToResponses.Convert(
            Chat("resposta", "pensando"), "m", 0.5, new BridgeOptions { Thinking = "events" });

        var reasoning = response["output"]![0]!.AsObject();
        Assert.Equal("reasoning", reasoning["type"]!.GetValue<string>());
        Assert.Equal("pensando", reasoning["content"]![0]!["text"]!.GetValue<string>());
        Assert.Empty(reasoning["summary"]!.AsArray());
        Assert.Equal("resposta", response["output"]![1]!["content"]![0]!["text"]!.GetValue<string>());
        Assert.Equal(10, response["usage"]!["input_tokens"]!.GetValue<int>());
    }

    [Fact]
    public void ThinkingOffViraTextoNoInicio()
    {
        var response = ChatToResponses.Convert(
            Chat("resposta", "pensando"), "m", 0.5, new BridgeOptions { Thinking = "off" });

        Assert.Single(response["output"]!.AsArray());
        Assert.Equal("pensando\n\nresposta", response["output"]![0]!["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void ToolCallsViramFunctionCallSemPrefixoDefault()
    {
        var calls = new JsonArray(new JsonObject
        {
            ["id"] = "call_9",
            ["type"] = "function",
            ["function"] = new JsonObject { ["name"] = "default.shell", ["arguments"] = "{\"command\":\"ls\"}" }
        });

        var response = ChatToResponses.Convert(Chat("", null, calls), "m", 0.5, new BridgeOptions());

        var call = response["output"]![0]!.AsObject();
        Assert.Equal("function_call", call["type"]!.GetValue<string>());
        Assert.Equal("shell", call["name"]!.GetValue<string>());
        Assert.Equal("call_9", call["call_id"]!.GetValue<string>());
    }

    [Fact]
    public void CoercaoExtraiToolNarradaNoTexto()
    {
        var chat = Chat("<tool_call>{\"name\":\"read\",\"arguments\":{\"filePath\":\"a\"}}</tool_call>");
        var response = ChatToResponses.Convert(
            chat, "m", 0.5, new BridgeOptions(),
            hadTools: true, announcedTools: new[] { "read" });

        Assert.Equal("function_call", response["output"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("read", response["output"]![0]!["name"]!.GetValue<string>());
    }
}
