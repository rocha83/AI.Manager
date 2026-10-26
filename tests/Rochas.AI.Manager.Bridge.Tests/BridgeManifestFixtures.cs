using Rochas.AI.Manager.Bridge.Contracts;

namespace Rochas.AI.Manager.Bridge.Tests;

internal static class BridgeManifestFixtures
{
    public static BridgeManifest Valid()
    {
        var manifest = new BridgeManifest
        {
            SchemaVersion = BridgeContractVersion.SchemaVersion,
            Experiment = new BridgeExperimentRef
            {
                Id = 42,
                Version = 3,
                Name = "WAR L2 - roxa",
                Statement = "cadastro de clientes"
            },
            Runner = new BridgeRunnerRef
            {
                Id = "local-q4",
                Capabilities = new List<string> { "dotnet", "web", "q4" }
            },
            Target = new BridgeTarget
            {
                BridgeUrl = "http://127.0.0.1:4124",
                UpstreamUrl = "http://127.0.0.1:4100",
                ModelId = "Gemma4-E4B-Q4",
                ContextWindow = 131072,
                MaxTokens = 16384,
                IsGate = true
            },
            Harness = new BridgeHarnessRef
            {
                ProfileVersionId = 7,
                Checksum = "sha256:abc",
                Mode = "BridgeManaged"
            },
            Stages = new List<BridgeStage>
            {
                new()
                {
                    Key = "L2",
                    Title = "WAR L2",
                    Order = 2,
                    MaxParallelSteps = 1,
                    OnFailure = "continue",
                    Gate = new BridgeGatePolicy { Policy = "default" },
                    Builds = new List<BridgeBuildSpec>
                    {
                        new()
                        {
                            Name = "build-slnx",
                            Command = "dotnet",
                            Args = new List<string> { "build", "{ws}/Rochas.OpenCodeBridge.slnx", "-c", "Release" },
                            TimeoutSec = 600
                        }
                    },
                    Preflight = new List<BridgePreflightCheck>
                    {
                        new() { Kind = "http-json", Url = "http://127.0.0.1:4100/health" },
                        new() { Kind = "bridge-status", Url = "http://127.0.0.1:4124/api/status" }
                    },
                    Steps = new List<BridgeStep>
                    {
                        new()
                        {
                            Key = "belts-build-roxa-bridge",
                            Kind = "dotnet-test",
                            Command = "dotnet",
                            Args = new List<string> { "run", "--project", "{ws}/Rochas.OpenCodeBridge.Test", "--", "--only", "E-belts-build-direct", "--belts", "roxa" },
                            TimeoutSec = 7200,
                            Retry = new BridgeRetryPolicy { MaxAttempts = 2, RetryOn = new List<string> { "timeout", "crash" } },
                            VerdictParser = "pass-fail-lines",
                            ScenarioSelector = new BridgeTestSelector { Suite = "belts-build", Cases = new List<string> { "roxa-build" } }
                        }
                    }
                }
            },
            Scenarios = new List<BridgeScenario>
            {
                new()
                {
                    Key = "roxa-build",
                    Title = "Cadastro de clientes e fornecedores",
                    Prompt = "Trabalhe em {ws}...",
                    Workspace = new BridgeScenarioWorkspace { Template = "belts-build", Clean = true },
                    Model = new BridgeScenarioModel { Role = "build", Temperature = 0.3, Thinking = "events" },
                    Tools = new BridgeScenarioTools { Mode = "required", Allow = new List<string> { "write" } },
                    Loop = new BridgeScenarioLoop { MaxTurns = 10, MaxNudges = 2, Review = true },
                    Assertions = new List<BridgeAssertion>
                    {
                        new() { Type = "fileCountAtLeast", Value = 3 },
                        new() { Type = "totalCharsAtLeast", Value = 2000 },
                        new() { Type = "fileExtensionCountAtLeast", Ext = ".cs", Value = 2 }
                    }
                }
            }
        };

        manifest.Experiment.ManifestHash = ManifestCanonicalizer.ComputeManifestHash(manifest);
        return manifest;
    }
}
