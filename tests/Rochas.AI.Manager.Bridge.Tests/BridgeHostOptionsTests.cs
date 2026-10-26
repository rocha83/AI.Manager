using Rochas.AI.Manager.Bridge.Hosting;

namespace Rochas.AI.Manager.Bridge.Tests;

public class BridgeHostOptionsTests
{
    [Fact]
    public void ParseCompleto()
    {
        var options = BridgeHostOptions.Parse(new[]
        {
            "--port", "4141",
            "--listen", "0.0.0.0",
            "--upstream", "http://127.0.0.1:4100",
            "--model", "Gemma4-E4B-Q4",
            "--api-key", "secret",
            "--artifacts", "/tmp/artifacts",
            "--mirror"
        });

        Assert.Equal(4141, options.Port);
        Assert.Equal("0.0.0.0", options.Listen);
        Assert.Equal("http://127.0.0.1:4100", options.UpstreamUrl);
        Assert.Equal("Gemma4-E4B-Q4", options.ModelId);
        Assert.Equal("secret", options.ApiKey);
        Assert.Equal("/tmp/artifacts", options.ArtifactRoot);
        Assert.False(options.IsGate);
        Assert.Equal("http://0.0.0.0:4141", options.BaseUrl);
    }

    [Fact]
    public void HelpNaoExigeValores()
    {
        var options = BridgeHostOptions.Parse(new[] { "--help" });

        Assert.True(options.ShowHelp);
    }

    [Fact]
    public void ArgumentoDesconhecidoFalhaRapido()
    {
        var exception = Assert.Throws<ArgumentException>(() => BridgeHostOptions.Parse(new[] { "--turbo" }));
        Assert.Contains("--turbo", exception.Message);
    }

    [Fact]
    public void FlagSemValorFalhaRapido()
    {
        Assert.Throws<ArgumentException>(() => BridgeHostOptions.Parse(new[] { "--port" }));
    }

    [Fact]
    public void PortaForaDaFaixaFalha()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BridgeHostOptions.Parse(new[] { "--port", "70000" }));
    }

    [Fact]
    public void UpstreamInvalidoFalha()
    {
        Assert.Throws<ArgumentException>(() => BridgeHostOptions.Parse(new[] { "--upstream", "ftp://x" }));
    }

    [Fact]
    public void ParseMotorEShim()
    {
        var options = BridgeHostOptions.Parse(new[]
        {
            "--temperature", "0.1",
            "--thinking", "off",
            "--max-tokens", "2048",
            "--context", "65536",
            "--no-harness",
            "--tool-shim", "json",
            "--tool-shim-max-string", "500",
            "--tool-shim-min-calls", "2"
        });

        Assert.Equal(0.1, options.Temperature);
        Assert.Equal("off", options.Thinking);
        Assert.Equal(2048, options.MaxTokens);
        Assert.Equal(65536, options.ContextWindow);
        Assert.False(options.Harness);
        Assert.Equal("json", options.ToolShimMode);
        Assert.Equal(500, options.ToolShimMaxString);
        Assert.Equal(2, options.ToolShimMinCalls);
    }

    [Fact]
    public void ShimVazioDesligaSemErro()
    {
        var options = BridgeHostOptions.Parse(new[] { "--tool-shim", "" });

        Assert.Equal(string.Empty, options.ToolShimMode);
        Assert.Equal(2000, options.ToolShimMaxString);
    }
}
