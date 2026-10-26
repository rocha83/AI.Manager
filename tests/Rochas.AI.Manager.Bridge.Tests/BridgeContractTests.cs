using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Bridge.Hosting;

namespace Rochas.AI.Manager.Bridge.Tests;

public class BridgeContractTests
{
    [Fact]
    public void SchemaAtualEhSuportado()
    {
        Assert.True(BridgeContractVersion.IsSchemaSupported(BridgeContractVersion.SchemaVersion));
        Assert.False(BridgeContractVersion.IsSchemaSupported("2.0"));
        Assert.False(BridgeContractVersion.IsSchemaSupported(null));
    }

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("1.0.5", true)]
    [InlineData("1.0", true)]
    [InlineData("1.1.0", false)]
    [InlineData("0.9.0", false)]
    [InlineData("2.0.0", false)]
    [InlineData("", false)]
    public void CompatibilidadeSegueSemVer(string version, bool expected)
    {
        Assert.Equal(expected, BridgeContractVersion.IsContractCompatible(version));
    }

    [Fact]
    public void ComponenteInformaContratoEVersao()
    {
        var info = BridgeComponent.Info();

        Assert.Equal(BridgeContractVersion.ComponentName, info.Name);
        Assert.Equal(BridgeContractVersion.ContractVersion, info.ContractVersion);
        Assert.Equal(BridgeContractVersion.SchemaVersion, info.SchemaVersion);
        Assert.Contains("manifest-dry-run", info.Features);
    }

    [Fact]
    public void StatusRefleteOPerfil()
    {
        var options = new BridgeHostOptions
        {
            Port = 4141,
            Listen = "0.0.0.0",
            UpstreamUrl = "http://127.0.0.1:4100",
            ModelId = "Gemma4-E4B-Q4",
            IsGate = false
        };

        var status = BridgeComponent.Status(options, DateTimeOffset.UtcNow.AddSeconds(-5));

        Assert.Equal(4141, status.Port);
        Assert.False(status.IsGate);
        Assert.Equal("mirror", status.Metadata!["role"]);
        Assert.True(status.UptimeSeconds >= 5);
    }
}
