using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Client;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Tests;

public class BridgeSseClientTests
{
    private static HttpClient Client(StubHttpHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:4140") };

    [Fact]
    public async Task ParseiaEventosEDataMultilinha()
    {
        const string sse = ": keep-alive\n" +
                           "event: response.output_text.delta\n" +
                           "data: {\"delta\":\"a\"}\n" +
                           "data: {\"delta\":\"b\"}\n" +
                           "\n" +
                           "event: done\n" +
                           "data: [DONE]\n" +
                           "\n";
        var handler = new StubHttpHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            }
        };
        var client = new BridgeSseClient(Client(handler));

        var events = new List<BridgeSseEvent>();
        await foreach (var item in client.StreamResponsesAsync(new JsonObject { ["model"] = "m" }))
        {
            events.Add(item);
        }

        Assert.Equal(2, events.Count);
        Assert.Equal("response.output_text.delta", events[0].EventName);
        Assert.Equal("{\"delta\":\"a\"}\n{\"delta\":\"b\"}", events[0].Data);
        Assert.Equal("done", events[1].EventName);
        Assert.Equal("[DONE]", events[1].Data);
        Assert.Equal("/v1/responses", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task EmiteUltimoEventoSemLinhaEmBranco()
    {
        const string sse = "event: delta\ndata: {\"x\":1}";
        var handler = new StubHttpHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sse, Encoding.UTF8, "text/event-stream")
            }
        };
        var client = new BridgeSseClient(Client(handler));

        var events = new List<BridgeSseEvent>();
        await foreach (var item in client.StreamChatCompletionsAsync(new JsonObject { ["model"] = "m" }))
        {
            events.Add(item);
        }

        var single = Assert.Single(events);
        Assert.Equal("delta", single.EventName);
        Assert.Equal("{\"x\":1}", single.Data);
        Assert.Equal("/v1/chat/completions", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ErroAntesDoStreamViraExcecaoTipada()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("""{"error":"missing token"}""", Encoding.UTF8, "application/json")
            }
        };
        var client = new BridgeSseClient(Client(handler));

        var exception = await Assert.ThrowsAsync<BridgeClientException>(async () =>
        {
            await foreach (var _ in client.StreamResponsesAsync(new JsonObject()))
            {
            }
        });

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("missing token", exception.Code);
    }
}
