using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Bridge.Flow;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ManifestCommandResolverTests
{
    [Fact]
    public void ResolveBuildComWorkspace()
    {
        var manifest = BridgeManifestFixtures.Valid();

        var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions
        {
            WorkspaceRoot = "/repo",
            Model = "Gemma4-E4B-Q4"
        });

        var build = Assert.Single(dryRun.Steps, step => step.Phase == "build");
        Assert.Equal("L2", build.StageKey);
        Assert.Equal("build-slnx", build.StepKey);
        Assert.Contains("/repo/Rochas.OpenCodeBridge.slnx", build.CommandLine);
        Assert.Equal("/repo", build.WorkingDirectory);
    }

    [Fact]
    public void ResolvePreflightEStepNaOrdem()
    {
        var manifest = BridgeManifestFixtures.Valid();

        var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions { WorkspaceRoot = "/repo" });

        Assert.Equal(new[] { "build", "preflight", "preflight", "step" }, dryRun.Steps.Select(step => step.Phase));
        Assert.Contains(dryRun.Steps, step => step.Phase == "preflight" && step.CommandLine.Contains("bridge-status"));
        var step = Assert.Single(dryRun.Steps, item => item.Phase == "step");
        Assert.Contains("--belts roxa", step.CommandLine);
        Assert.Empty(step.DependsOn);
    }

    [Fact]
    public void HashCalculadoConfereComOManifesto()
    {
        var manifest = BridgeManifestFixtures.Valid();

        var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions { WorkspaceRoot = "/repo" });
        Assert.True(dryRun.ManifestHashMatches);
        Assert.Equal(manifest.Experiment.ManifestHash, dryRun.ManifestHashComputed);
        Assert.Empty(dryRun.Issues);
    }

    [Fact]
    public void HashAdulteradoViraIssue()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Experiment.ManifestHash = "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        var dryRun = ManifestCommandResolver.Resolve(manifest);

        Assert.False(dryRun.ManifestHashMatches);
        Assert.Contains(dryRun.Issues, issue => issue.Code == "manifest.hash.mismatch");
    }

    [Fact]
    public void PlaceholderDesconhecidoViraIssue()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Stages[0].Builds[0].Args.Add("{qualquer}");

        var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions { WorkspaceRoot = "/repo" });

        Assert.Contains(dryRun.Issues, issue => issue.Code == "dryRun.placeholder.unknown");
    }

    [Fact]
    public void PlaceholderDeCaseSemValorFicaLiteralComIssue()
    {
        var manifest = BridgeManifestFixtures.Valid();
        manifest.Stages[0].Steps[0].Args.Add("{case}");

        var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions { WorkspaceRoot = "/repo" });

        Assert.Contains(dryRun.Issues, issue => issue.Code == "dryRun.placeholder.unresolved");
        Assert.Contains("{case}", dryRun.Steps.Single(step => step.Phase == "step").CommandLine);
    }
}
