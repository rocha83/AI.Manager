using Rochas.AI.Manager.Bridge;
using Xunit;

namespace Rochas.AI.Manager.Tests;

public sealed class HarnessTailsTests
{
    [Fact]
    public void BehaviorMantemSoODeltaDoCanal()
    {
        string behavior = HarnessTails.Behavior;

        Assert.Contains("delta da bridge", behavior);
        Assert.Contains("TOOLS PRIMEIRO", behavior);
        Assert.Contains("PERSISTENCIA", behavior);
        Assert.Contains("PRIORIDADE", behavior);
        // O que a base ja cobre nao se repete na cauda.
        Assert.DoesNotContain("auto-apresente", behavior);
        Assert.DoesNotContain("max 2 tentativas", behavior);
    }

    [Fact]
    public void MainHarnessCobreEscritaSemLoopEFormato()
    {
        // A regra de escrita/workspace migrou da cauda para a base enxuta.
        Assert.Contains("write só para arquivo novo", MainHarness.Text);
        Assert.Contains("edit/sed", MainHarness.Text);
        Assert.Contains("um nível abaixo", MainHarness.Text);
        Assert.Contains("repita a mesma chamada/caminho", MainHarness.Text);
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
