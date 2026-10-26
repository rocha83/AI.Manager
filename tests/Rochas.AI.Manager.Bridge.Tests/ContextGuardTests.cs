using Rochas.AI.Manager.Bridge.Transport;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ContextGuardTests
{
    [Fact]
    public void DesligadaNuncaDispara()
    {
        var tripped = ContextGuard.Tripped(
            """{"usage":{"prompt_tokens":999}}""",
            new ContextGuardSettings(false, 0.95, 100),
            out _,
            out _);

        Assert.False(tripped);
    }

    [Fact]
    public void SemContextoTambemNaoDispara()
    {
        var tripped = ContextGuard.Tripped(
            """{"usage":{"prompt_tokens":999}}""",
            new ContextGuardSettings(true, 0.95, 0),
            out _,
            out _);

        Assert.False(tripped);
    }

    [Fact]
    public void DisparaNoLimiarEInformaFracao()
    {
        var tripped = ContextGuard.Tripped(
            """{"usage":{"prompt_tokens":96}}""",
            new ContextGuardSettings(true, 0.95, 100),
            out var promptTokens,
            out var fraction);

        Assert.True(tripped);
        Assert.Equal(96, promptTokens);
        Assert.Equal(0.96, fraction, 3);
    }

    [Fact]
    public void JsonInvalidoNaoQuebra()
    {
        var tripped = ContextGuard.Tripped(
            "nao e json",
            new ContextGuardSettings(true, 0.95, 100),
            out var promptTokens,
            out var fraction);

        Assert.False(tripped);
        Assert.Equal(0, promptTokens);
        Assert.Equal(0, fraction);
    }

    [Fact]
    public void PayloadTemCodigoDeCompactacao()
    {
        var payload = ContextGuard.BuildPayload(96, 0.96, 100);

        Assert.Equal("context_compact_required", payload["error"]!["code"]!.GetValue<string>());
        Assert.Contains("96", payload["error"]!["message"]!.GetValue<string>());
    }
}
