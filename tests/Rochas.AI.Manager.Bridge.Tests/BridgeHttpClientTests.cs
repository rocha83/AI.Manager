using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Client;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Tests;

internal sealed class StubHttpHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.OK);

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        if (request.Content is not null)
            LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
        return Responder(request);
    }
}

public class BridgeHttpClientTests
{
    private static HttpClient Client(StubHttpHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:4140") };

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task LeStatusDoHost()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""
                {"name":"Rochas.AI.Manager.Bridge","version":"1.0.0","contractVersion":"1.0.0",
                 "schemaVersion":"1.0","startedAtUtc":"2026-10-25T00:00:00+00:00","uptimeSeconds":7,
                 "upstreamUrl":"http://127.0.0.1:4100","model":"Gemma4-E4B-Q4","isGate":true,
                 "port":4140,"listen":"127.0.0.1"}
                """)
        };
        var client = new BridgeHttpClient(Client(handler));

        var status = await client.GetStatusAsync();

        Assert.Equal("Gemma4-E4B-Q4", status.Model);
        Assert.True(status.IsGate);
        Assert.Equal("/api/status", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
    }

    [Fact]
    public async Task ErroHttpViraExcecaoTipada()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""{"error":"missing token"}""", HttpStatusCode.Unauthorized)
        };
        var client = new BridgeHttpClient(Client(handler));

        var exception = await Assert.ThrowsAsync<BridgeClientException>(() => client.GetStatusAsync());

        Assert.Equal(401, exception.StatusCode);
        Assert.Equal("missing token", exception.Code);
    }

    [Fact]
    public async Task EnviaManifestoParaValidacaoEmCamelCase()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""{"isValid":true,"errors":[],"warnings":[]}""")
        };
        var client = new BridgeHttpClient(Client(handler));

        var result = await client.ValidateManifestAsync(BridgeManifestFixtures.Valid());

        Assert.True(result.IsValid);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("/api/manifest/validate", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"schemaVersion\"", handler.LastBody);
        Assert.Contains("\"stages\"", handler.LastBody);
    }

    [Fact]
    public async Task DryRunDevolveComandos()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""
                {"schemaVersion":"1.0","manifestHash":"sha256:a","manifestHashComputed":"sha256:a",
                 "manifestHashMatches":true,"steps":[],"issues":[]}
                """)
        };
        var client = new BridgeHttpClient(Client(handler));

        var dryRun = await client.DryRunManifestAsync(BridgeManifestFixtures.Valid());

        Assert.True(dryRun.ManifestHashMatches);
        Assert.Equal("/api/manifest/dry-run", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ContratoExigidoDivergenteFalha()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""
                {"name":"bridge","version":"2.0.0","contractVersion":"2.0.0","schemaVersion":"2.0",
                 "supportedSchemaVersions":["2.0"],"features":[]}
                """)
        };
        var client = new BridgeHttpClient(Client(handler), new BridgeClientOptions { RequiredContractVersion = "1.0.0" });

        var exception = await Assert.ThrowsAsync<BridgeClientException>(() => client.GetContractAsync());
        Assert.Equal("client.contract.mismatch", exception.Code);
    }

    [Fact]
    public async Task ExecutaToolNoHost()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""{"success":true,"output":"ok","durationMs":2}""")
        };
        var client = new BridgeHttpClient(Client(handler));

        var response = await client.ExecuteToolAsync(new BridgeToolRequest
        {
            Name = "read",
            Arguments = new JsonObject { ["path"] = "a.txt" }
        });

        Assert.True(response.Success);
        Assert.Equal("ok", response.Output);
        Assert.Equal("/api/tools/execute", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task BearerEhEnviadoQuandoConfigurado()
    {
        var handler = new StubHttpHandler
        {
            Responder = _ => Json("""{"isValid":true,"errors":[],"warnings":[]}""")
        };
        var client = new BridgeHttpClient(Client(handler), new BridgeClientOptions { ApiKey = "secret" });

        await client.ValidateManifestAsync(BridgeManifestFixtures.Valid());

        Assert.Equal("Bearer", handler.LastRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("secret", handler.LastRequest.Headers.Authorization.Parameter);
    }
}
