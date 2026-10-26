using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ManifestCanonicalizerTests
{
    [Fact]
    public void CanonicalizaComChavesOrdenadasESemEspacos()
    {
        var node = JsonNode.Parse("""{"b":1,"a":{"d":2,"c":3}}""");

        var canonical = ManifestCanonicalizer.Canonicalize(node);

        Assert.Equal("""{"a":{"c":3,"d":2},"b":1}""", canonical);
    }

    [Fact]
    public void HashNaoDependeDaOrdemDasChaves()
    {
        var first = ManifestCanonicalizer.Canonicalize(JsonNode.Parse("""{"a":1,"b":2}"""));
        var second = ManifestCanonicalizer.Canonicalize(JsonNode.Parse("""{"b":2,"a":1}"""));

        Assert.Equal(
            ManifestCanonicalizer.ComputeHash(first),
            ManifestCanonicalizer.ComputeHash(second));
    }

    [Fact]
    public void HashDoManifestoIgnoraOCampoManifestHash()
    {
        var manifest = BridgeManifestFixtures.Valid();
        var hash = ManifestCanonicalizer.ComputeManifestHash(manifest);

        manifest.Experiment.ManifestHash = "sha256:deadbeef";

        Assert.Equal(hash, ManifestCanonicalizer.ComputeManifestHash(manifest));
    }

    [Fact]
    public void HashMudaQuandoOConteudoMuda()
    {
        var manifest = BridgeManifestFixtures.Valid();
        var hash = ManifestCanonicalizer.ComputeManifestHash(manifest);

        manifest.Scenarios[0].Prompt += " ajuste";

        Assert.NotEqual(hash, ManifestCanonicalizer.ComputeManifestHash(manifest));
    }

    [Fact]
    public void VerificaHashInformado()
    {
        var manifest = BridgeManifestFixtures.Valid();

        Assert.True(ManifestCanonicalizer.VerifyManifestHash(manifest));

        manifest.Experiment.ManifestHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";
        Assert.False(ManifestCanonicalizer.VerifyManifestHash(manifest));
    }
}
