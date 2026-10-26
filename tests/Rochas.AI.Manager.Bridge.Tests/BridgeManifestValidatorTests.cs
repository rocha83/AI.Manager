using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Tests;

public class BridgeManifestValidatorTests
{
    [Fact]
    public void ManifestoValidoPassa()
    {
        var result = BridgeManifestValidator.Validate(BridgeManifestFixtures.Valid());

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => $"{error.Code}: {error.Message}")));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void SchemaDesconhecidoBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.SchemaVersion = "2.0";

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "schema.unsupported");
    }

    [Fact]
    public void StepKindDesconhecidoBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Stages[0].Steps[0].Kind = "rm-rf";

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "step.kind.unknown");
    }

    [Fact]
    public void StageDuplicadoBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Stages.Add(new BridgeStage { Key = manifest.Stages[0].Key, Order = 3 });

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "stage.key.duplicate");
    }

    [Fact]
    public void StepDuplicadoNoStageBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Stages[0].Steps.Add(new BridgeStep
        {
            Key = manifest.Stages[0].Steps[0].Key,
            Kind = "shell",
            Command = "echo"
        });

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "step.key.duplicate");
    }

    [Fact]
    public void CenarioInexistenteNoSeletorBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Stages[0].Steps[0].ScenarioSelector!.Cases.Add("nao-existe");

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "step.scenario.unknown");
    }

    [Fact]
    public void DependenciaInexistenteBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Stages[0].Steps[0].DependsOn.Add("nao-existe");

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "step.dependsOn.unknown");
    }

    [Fact]
    public void HashAdulteradoBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Experiment.ManifestHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "manifest.hash.mismatch");
    }

    [Fact]
    public void UrlInvalidaBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Target.UpstreamUrl = "not-a-url";

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "target.upstreamUrl.invalid");
    }

    [Fact]
    public void AssertionDesconhecidaBloqueia()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Scenarios[0].Assertions.Add(new BridgeAssertion { Type = "magic" });

        var result = BridgeManifestValidator.Validate(manifest);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "scenario.assertion.unknown");
    }

    [Fact]
    public void ManifestoNuloBloqueia()
    {
        var result = BridgeManifestValidator.Validate(null);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "manifest.null");
    }
}
