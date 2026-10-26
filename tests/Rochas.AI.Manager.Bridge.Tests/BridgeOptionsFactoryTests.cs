using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Tests;

public class BridgeOptionsFactoryTests
{
    private static BridgeModelSettings Model(string toolMode = "", bool usesCoercion = false) =>
        new("off", 0.1, 4096, 65536, usesCoercion) { ToolMode = toolMode };

    [Fact]
    public void CoercaoLegadaViraNarrado()
    {
        var options = BridgeOptionsFactory.FromModel(Model(usesCoercion: true));

        Assert.True(options.CoerceNarrated);
        Assert.Equal(string.Empty, options.ToolShimMode);
    }

    [Fact]
    public void ToolModeShimLigaGrammarEDesligaNarrado()
    {
        var options = BridgeOptionsFactory.FromModel(Model(toolMode: "shim", usesCoercion: true));

        Assert.Equal("json", options.ToolShimMode);
        Assert.False(options.CoerceNarrated);
    }

    [Fact]
    public void ToolModeNativeDesligaCoercaoEShim()
    {
        var options = BridgeOptionsFactory.FromModel(Model(toolMode: "native", usesCoercion: true));

        Assert.Equal(string.Empty, options.ToolShimMode);
        Assert.False(options.CoerceNarrated);
    }

    [Fact]
    public void ToolModeNarratedMantemCoercaoSemShim()
    {
        var options = BridgeOptionsFactory.FromModel(Model(toolMode: "narrated", usesCoercion: false));

        Assert.Equal(string.Empty, options.ToolShimMode);
        Assert.True(options.CoerceNarrated);
    }

    [Fact]
    public void ConteudoDeHarnessVenceODefault()
    {
        var options = BridgeOptionsFactory.FromModel(
            Model(),
            new BridgeHarnessContent("behavior-x", "tools-y"));

        Assert.Equal("behavior-x", options.BehaviorTail);
        Assert.Equal("tools-y", options.ToolsTail);
    }
}
