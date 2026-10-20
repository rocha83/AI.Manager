using Rochas.AI.Manager.Bridge;
using Xunit;

namespace Rochas.AI.Manager.Tests;

public sealed class HarnessTailsTests
{
    [Fact]
    public void BehaviorTemRegraDeEscritaContraRepeticao()
    {
        string behavior = HarnessTails.Behavior;

        Assert.Contains("COMPORTAMENTO (escrita)", behavior);
        Assert.Contains("edit no trecho exato", behavior);
        Assert.Contains("mesmos argumentos", behavior);
        Assert.Contains("max 2 tentativas", behavior);
    }

    [Fact]
    public void BehaviorMantemWorkspaceEFormato()
    {
        Assert.Contains("COMPORTAMENTO (workspace)", HarnessTails.Behavior);
        Assert.Contains("um nivel abaixo", HarnessTails.Behavior);
        Assert.Contains("COMPORTAMENTO (formato)", HarnessTails.Behavior);
        Assert.Contains("$...$", HarnessTails.Behavior);
    }

    [Fact]
    public void ToolsNaoTemFragmentoConcatenado()
    {
        Assert.DoesNotContain("digitando", HarnessTails.Tools);
        Assert.DoesNotContain("nao existe<", HarnessTails.Tools);
        Assert.Contains("MODO DE USO DAS TOOLS", HarnessTails.Tools);
    }

    [Fact]
    public void AvisoAntiRepeatDizComoSairDoLoop()
    {
        Assert.Contains("edit no trecho exato", AntiRepeat.DefaultNotice);
        Assert.Contains("Nao repita", AntiRepeat.DefaultNotice);
        Assert.Contains("read/grep", AntiRepeat.DefaultNotice);
    }
}
