using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Transport;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ToolShimTests
{
    private static JsonObject Body(int? minCalls = null) => new()
    {
        ["model"] = "m",
        ["messages"] = new JsonArray(
            new JsonObject { ["role"] = "system", ["content"] = "seja util" },
            new JsonObject { ["role"] = "user", ["content"] = "leia a.txt" }),
        ["tools"] = new JsonArray(new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = "read",
                ["description"] = "le arquivo",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["path"] = new JsonObject { ["type"] = "string" }
                    },
                    ["required"] = new JsonArray("path")
                }
            }
        }),
        ["tool_choice"] = "auto",
        ["stream"] = true,
        ["max_tokens"] = 99
    };

    [Fact]
    public void ApplyRemoveToolsEInjetaGrammarNoSystem()
    {
        var body = Body();
        var applied = ToolShim.Apply(body, maxString: 500, minCalls: 0, maxTokens: 1234, out var reason);

        Assert.True(applied, reason);
        Assert.False(body.ContainsKey("tools"));
        Assert.False(body.ContainsKey("tool_choice"));
        Assert.False(body["stream"]!.GetValue<bool>());
        Assert.Equal(1234, body["max_tokens"]!.GetValue<int>());
        Assert.False(body["chat_template_kwargs"]!["enable_thinking"]!.GetValue<bool>());

        var grammar = body["grammar"]!.GetValue<string>();
        Assert.Contains("root ::=", grammar);
        Assert.Contains("read", grammar);
        Assert.Contains("path", grammar);

        var system = body["messages"]![0]!["content"]!.GetValue<string>();
        Assert.Contains("[TOOL SHIM JSON]", system);
        Assert.Contains("\"read\"", system);
    }

    [Fact]
    public void ApplySemToolsNaoAtua()
    {
        var body = Body();
        body.Remove("tools");

        var applied = ToolShim.Apply(body, 500, 0, 0, out var reason);

        Assert.False(applied);
        Assert.Equal("sem tools", reason);
    }

    [Fact]
    public void MinCallsEscondeFinalAteTerHistorico()
    {
        var semHistorico = Body();
        ToolShim.Apply(semHistorico, 500, minCalls: 1, maxTokens: 0, out _);
        Assert.DoesNotContain("final ::=", semHistorico["grammar"]!.GetValue<string>());

        var comHistorico = Body();
        comHistorico["messages"]!.AsArray().Add(new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = null,
            ["tool_calls"] = new JsonArray(new JsonObject
            {
                ["id"] = "call_1",
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = "read", ["arguments"] = "{\"path\":\"a.txt\"}" }
            })
        });
        ToolShim.Apply(comHistorico, 500, minCalls: 1, maxTokens: 0, out _);
        Assert.Contains("final ::=", comHistorico["grammar"]!.GetValue<string>());
    }

    [Fact]
    public void TransformResponseConverteEnvelopeEmToolCall()
    {
        var response = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = "{\"name\":\"read\",\"arguments\":{\"path\":\"a.txt\"}}"
                },
                ["finish_reason"] = "stop"
            })
        };

        ToolShim.TransformResponse(response);

        var message = response["choices"]![0]!["message"]!.AsObject();
        var call = message["tool_calls"]!.AsArray()[0]!.AsObject();
        Assert.Equal("read", call["function"]!["name"]!.GetValue<string>());
        Assert.Equal("{\"path\":\"a.txt\"}", call["function"]!["arguments"]!.GetValue<string>());
        Assert.Equal("", message["content"]!.GetValue<string>());
        Assert.Equal("tool_calls", response["choices"]![0]!["finish_reason"]!.GetValue<string>());
    }

    [Fact]
    public void TransformResponseFinalViraContentLimpo()
    {
        var response = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["role"] = "assistant",
                    ["content"] = "{\"final\":\"pronto\"}"
                },
                ["finish_reason"] = "stop"
            })
        };

        ToolShim.TransformResponse(response);

        var message = response["choices"]![0]!["message"]!.AsObject();
        Assert.Equal("pronto", message["content"]!.GetValue<string>());
        Assert.False(message.ContainsKey("tool_calls"));
        Assert.Equal("stop", response["choices"]![0]!["finish_reason"]!.GetValue<string>());
    }

    [Fact]
    public void TransformResponseProsaPassaIntacta()
    {
        var response = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject { ["role"] = "assistant", ["content"] = "texto normal" },
                ["finish_reason"] = "stop"
            })
        };

        ToolShim.TransformResponse(response);

        Assert.Equal("texto normal", response["choices"]![0]!["message"]!["content"]!.GetValue<string>());
        Assert.False(response["choices"]![0]!["message"]!.AsObject().ContainsKey("tool_calls"));
    }
}
