using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Transport;

namespace Rochas.AI.Manager.Bridge.Tests;

public class BridgeProxyTests
{
    private static HttpClient Client(StubHttpHandler handler) => new(handler);

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static BridgeProxy Proxy(StubHttpHandler handler) =>
        new(Client(handler), new BridgeProxyOptions
        {
            UpstreamUrl = "http://127.0.0.1:4100",
            ModelId = "Gemma4-E4B-Q4"
        });

    private static JsonObject Request() => new()
    {
        ["model"] = "qualquer",
        ["instructions"] = "seja util",
        ["input"] = new JsonArray(new JsonObject
        {
            ["type"] = "message",
            ["role"] = "user",
            ["content"] = new JsonArray(new JsonObject { ["type"] = "input_text", ["text"] = "leia a.txt" })
        }),
        ["tools"] = new JsonArray(new JsonObject
        {
            ["type"] = "function",
            ["name"] = "read",
            ["description"] = "le arquivo",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject { ["path"] = new JsonObject { ["type"] = "string" } },
                ["required"] = new JsonArray("path")
            }
        }),
        ["temperature"] = 0.3
    };

    [Fact]
    public async Task CompleteTraduzResponsesParaChatEVolta()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""
                {"id":"chatcmpl-1","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":12,"completion_tokens":3}}
                """)
        };
        var result = await Proxy(handler).CompleteAsync(Request(), new BridgeOptions());

        Assert.Equal("response", result.Body["object"]!.GetValue<string>());
        Assert.Equal("completed", result.Body["status"]!.GetValue<string>());
        Assert.Equal("ok", result.Body["output"]![0]!["content"]![0]!["text"]!.GetValue<string>());

        var sent = JsonNode.Parse(handler.LastBody!)!.AsObject();
        Assert.Equal("/v1/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal("Gemma4-E4B-Q4", sent["model"]!.GetValue<string>());
        Assert.False(sent["stream"]!.GetValue<bool>());
        Assert.NotNull(sent["tools"]);
        Assert.Contains("TOOLS", sent["messages"]![0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public async Task CompleteComShimConverteEnvelopeEmFunctionCall()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""
                {"id":"chatcmpl-1","choices":[{"index":0,
                 "message":{"role":"assistant","content":"{\"name\":\"read\",\"arguments\":{\"path\":\"a.txt\"}}"},
                 "finish_reason":"stop"}],"usage":{"prompt_tokens":12,"completion_tokens":3}}
                """)
        };
        var options = new BridgeOptions { ToolShimMode = "json" };

        var result = await Proxy(handler).CompleteAsync(Request(), options);

        var sent = JsonNode.Parse(handler.LastBody!)!.AsObject();
        Assert.False(sent.ContainsKey("tools"));
        Assert.NotNull(sent["grammar"]);

        var call = result.Body["output"]![0]!.AsObject();
        Assert.Equal("function_call", call["type"]!.GetValue<string>());
        Assert.Equal("read", call["name"]!.GetValue<string>());
        Assert.Contains("a.txt", call["arguments"]!.GetValue<string>());
    }

    [Fact]
    public async Task ContextGuardDispara413()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""
                {"id":"chatcmpl-1","choices":[{"index":0,"message":{"role":"assistant","content":"ok"},"finish_reason":"stop"}],
                 "usage":{"prompt_tokens":95,"completion_tokens":1}}
                """)
        };
        var options = new BridgeOptions { ContextGuard = true, ContextTokens = 100, ContextThreshold = 0.95 };

        var exception = await Assert.ThrowsAsync<BridgeProxyException>(
            () => Proxy(handler).CompleteAsync(Request(), options));

        Assert.Equal(413, exception.StatusCode);
        Assert.Contains("context_compact_required", exception.ResponseBody);
    }

    [Fact]
    public async Task ForwardChatComShimConverteResposta()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""
                {"id":"chatcmpl-1","choices":[{"index":0,
                 "message":{"role":"assistant","content":"{\"name\":\"read\",\"arguments\":{\"path\":\"b.txt\"}}"},
                 "finish_reason":"stop"}]}
                """)
        };
        var request = new JsonObject
        {
            ["model"] = "m",
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "system", ["content"] = "sys" },
                new JsonObject { ["role"] = "user", ["content"] = "leia" }),
            ["tools"] = new JsonArray(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = "read",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject { ["path"] = new JsonObject { ["type"] = "string" } },
                        ["required"] = new JsonArray("path")
                    }
                }
            })
        };

        var result = await Proxy(handler).ForwardChatAsync(
            request,
            new BridgeOptions { ToolShimMode = "json" });

        var message = result.Body["choices"]![0]!["message"]!.AsObject();
        Assert.Equal("read", message["tool_calls"]![0]!["function"]!["name"]!.GetValue<string>());
        Assert.Equal("{\"path\":\"b.txt\"}", message["tool_calls"]![0]!["function"]!["arguments"]!.GetValue<string>());
    }

    [Fact]
    public async Task BreakerTripNaoChamaUpstream()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => throw new InvalidOperationException("nao deveria chamar o upstream")
        };
        var options = new BridgeOptions
        {
            Harness = false,
            BreakerSoft = 3,
            BreakerHard = 6
        };

        var result = await Proxy(handler).CompleteAsync(TripRequest(), options);

        Assert.Null(handler.LastRequest);
        string text = result.Body["output"]![0]!["content"]![0]!["text"]!.GetValue<string>();
        Assert.Contains("BRIDGE-BREAKER", text);
        Assert.Contains("encerrou este turno", text);
    }

    private static JsonObject TripRequest()
    {
        var input = new JsonArray();
        for (int i = 0; i < 6; i++)
        {
            string id = "call_" + i;
            input.Add(new JsonObject
            {
                ["type"] = "function_call",
                ["call_id"] = id,
                ["name"] = "read",
                ["arguments"] = new JsonObject { ["path"] = "/alpha.txt" }.ToJsonString()
            });
            input.Add(new JsonObject
            {
                ["type"] = "function_call_output",
                ["call_id"] = id,
                ["output"] = "No such file or directory"
            });
        }

        return new JsonObject
        {
            ["model"] = "m",
            ["instructions"] = "sys",
            ["input"] = input
        };
    }

    [Fact]
    public async Task UpstreamErroPreservaStatus()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""{"error":{"message":"boom"}}""", HttpStatusCode.BadGateway)
        };

        var exception = await Assert.ThrowsAsync<BridgeProxyException>(
            () => Proxy(handler).CompleteAsync(Request(), new BridgeOptions()));

        Assert.Equal(502, exception.StatusCode);
        Assert.Contains("boom", exception.ResponseBody);
    }
}
