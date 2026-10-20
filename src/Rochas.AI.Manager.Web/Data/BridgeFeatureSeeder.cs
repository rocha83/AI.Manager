using Rochas.AI.Manager.Application.Helpers;
using Rochas.AI.Manager.Domain.Entities;
using Rochas.AI.Manager.Domain.Experiments;
using Rochas.AI.Manager.Domain.Harness;
using Rochas.AI.Manager.Domain.Models;
using Rochas.AI.Manager.Domain.Profiles;
using Rochas.Data.Specification.Interfaces;
using System.Threading;

namespace Rochas.AI.Manager.Web.Data;

public static class BridgeFeatureSeeder
{
    public static async Task SeedAsync(
        IGenericRepository<ModelArtifact> modelRepository,
        IGenericRepository<Agent> agentRepository,
        IGenericRepository<ModelBinding> bindingRepository,
        IGenericRepository<HarnessProfile> profileRepository,
        IGenericRepository<HarnessProfileVersion> versionRepository,
        IGenericRepository<HarnessAttachment> attachmentRepository,
        IGenericRepository<ToolPolicy> policyRepository,
        IGenericRepository<InstructionProfile> instructionRepository,
        IGenericRepository<TrainingPattern> patternRepository,
        IGenericRepository<Experiment> experimentRepository)
    {
        // ModelArtifact
        var existingModels = await modelRepository.Query(new ModelArtifact());
        if (!existingModels.Any(model => model.Name == "Gemma4 12B Q4_K_M"))
        {
            await modelRepository.Add(new ModelArtifact { Name = "Gemma4 12B Q4_K_M", Family = "Gemma4", Variant = "12b", Format = "gguf", IsTrainable = true, Status = "Approved" });
        }
        if (!existingModels.Any(model => model.Name == "Gemma4 E2B Q3_K_M"))
        {
            await modelRepository.Add(new ModelArtifact { Name = "Gemma4 E2B Q3_K_M", Family = "Gemma4", Variant = "e2b", Format = "gguf", IsTrainable = false, Status = "Approved" });
        }

        if (!existingModels.Any(model => model.Name == "Gemma4 12B Adapted (GGUF)"))
        {
            await modelRepository.Add(new ModelArtifact { Name = "Gemma4 12B Adapted (GGUF)", Family = "Gemma4", Variant = "12b", Format = "gguf", IsTrainable = false, Status = "Draft" });
        }
        if (!existingModels.Any(model => model.Name == "Gemma4 E2B Adapted (LiteRT)"))
        {
            await modelRepository.Add(new ModelArtifact { Name = "Gemma4 E2B Adapted (LiteRT)", Family = "Gemma4", Variant = "e2b", Format = "litertlm", IsTrainable = false, Status = "Draft" });
        }

        // ModelBinding
        var existingBindings = await bindingRepository.Query(new ModelBinding());
        if (!existingBindings.Any(binding => binding.ModelId == "gemma-4-12b"))
        {
            await bindingRepository.Add(new ModelBinding { Name = "Gemma4 12B (bridge GPU)", Provider = "openai", ModelId = "gemma-4-12b", MaxTokens = 32768, ContextWindow = 163840, Thinking = "on", Temperature = 0.5 });
        }
        if (!existingBindings.Any(binding => binding.ModelId == "gemma-4-e2b"))
        {
            await bindingRepository.Add(new ModelBinding { Name = "Gemma4 E2B (bridge CPU)", Provider = "llama", ModelId = "gemma-4-e2b", MaxTokens = 16384, ContextWindow = 65536, Thinking = "off", Temperature = 0.3 });
        }

        // Agent (6 roles)
        var existingAgents = await agentRepository.Query(new Agent());
        var agentDefinitions = new (string Name, string Model, string BridgeUrl, double Temperature, string Thinking, string Mode, string Tools)[]
        {
            ("plan", "openai/gemma-4-12b", "http://localhost:4124", 0.5, "on", "Plan", "read,grep,glob,question,subagent"),
            ("build", "openai/gemma-4-12b", "http://localhost:4124", 0.5, "on", "Build", "read,write,edit,glob,grep,shell,execute,subagent"),
            ("orch", "openai/gemma-4-12b", "http://localhost:4124", 0.5, "on", "Orch", "subagent,read,grep"),
            ("moe", "openai/gemma-4-12b", "http://localhost:4124", 0.8, "on", "MoE", "read,write,edit,glob,grep,shell,subagent"),
            ("scripter", "openai/gemma-4-12b", "http://localhost:4124", 0.3, "on", "Scripter", "read,write,edit,glob,grep,subagent"),
            ("exec", "llama/gemma-4-e2b", "http://localhost:4125", 0.3, "off", "Exec", "read,write,edit,glob,grep,shell,execute"),
        };
        foreach (var (agentName, model, bridgeUrl, temperature, thinking, mode, tools) in agentDefinitions)
        {
            var existing = existingAgents.FirstOrDefault(agent => agent.Name == agentName);
            if (existing is null)
            {
                await agentRepository.Add(new Agent { Name = agentName, Model = model, BridgeUrl = bridgeUrl, Temperature = temperature, Thinking = thinking, Mode = mode, Tools = tools });
                continue;
            }
            // Seed e fonte da verdade do enunciado: migra o token legado `task`
            // e garante `subagent` nas roles que delegam.
            var synced = ToolListNormalizer.Normalize(existing.Tools, tools, agentName != "exec");
            if (synced == existing.Tools) continue;
            var oldAgent = new Agent();
            foreach (var property in typeof(Agent).GetProperties()) property.SetValue(oldAgent, property.GetValue(existing));
            existing.Tools = synced;
            await agentRepository.Update(existing, oldAgent);
        }

        // HarnessProfile
        var existingProfiles = await profileRepository.Query(new HarnessProfile());
        int profileId;
        if (existingProfiles.Any(profile => profile.Name == "OpenCodeBridge Agents"))
        {
            profileId = existingProfiles.First(profile => profile.Name == "OpenCodeBridge Agents").Id;
        }
        else
        {
            var profile = new HarnessProfile { Name = "OpenCodeBridge Agents", Description = "Harness principal dos 6 agentes (Plan/Build/Orch/MoE/Scripter/Exec)", Status = "Approved" };
            await profileRepository.Add(profile);
            profileId = profile.Id;
            existingProfiles = await profileRepository.Query(new HarnessProfile());
        }

        // Version
        var existingVersions = await versionRepository.Query(new HarnessProfileVersion());
        int versionId;
        if (existingVersions.Any(version => version.ProfileId == profileId && version.Version == 1))
        {
            versionId = existingVersions.First(version => version.ProfileId == profileId && version.Version == 1).Id;
        }
        else
        {
            var version = new HarnessProfileVersion { ProfileId = profileId, Version = 1, Mode = "ClientNative", ApprovedBy = "seeder", ApprovedAt = DateTime.UtcNow };
            await versionRepository.Add(version);
            versionId = version.Id;
        }

        // Attachments (enunciados): seed e fonte da verdade; sincroniza quando o
        // recurso embutido muda (o conteudo antigo nao fica preso no banco).
        var existingAttachments = await attachmentRepository.Query(new HarnessAttachment());
        {
            var assembly = typeof(BridgeFeatureSeeder).Assembly;
            string ReadResource(string resourceName)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null) return string.Empty;
                using var reader = new System.IO.StreamReader(stream);
                return reader.ReadToEnd();
            }

            async Task SyncAttachment(string kind, string content)
            {
                if (string.IsNullOrWhiteSpace(content)) return;
                var current = existingAttachments.FirstOrDefault(attachment =>
                    attachment.VersionId == versionId && attachment.Kind == kind && attachment.Role == "general");
                if (current is null)
                {
                    await attachmentRepository.Add(new HarnessAttachment { VersionId = versionId, Kind = kind, Role = "general", Content = content, CharCount = content.Length });
                    return;
                }
                if (current.Content == content) return;
                var oldAttachment = new HarnessAttachment();
                foreach (var property in typeof(HarnessAttachment).GetProperties()) property.SetValue(oldAttachment, property.GetValue(current));
                current.Content = content;
                current.CharCount = content.Length;
                await attachmentRepository.Update(current, oldAttachment);
            }

            var systemPrompt = ReadResource("Rochas.AI.Manager.Web.Data.harness-system.md");
            if (string.IsNullOrWhiteSpace(systemPrompt)) systemPrompt = "Voce e um arquiteto desenvolvedor de software generalista.";
            await SyncAttachment("Core", systemPrompt);
            await SyncAttachment("Tools", ReadResource("Rochas.AI.Manager.Web.Data.harness-tools.json"));
            await SyncAttachment("Nucleo", ReadResource("Rochas.AI.Manager.Web.Data.harness-nucleo.md"));
        }

        // ToolPolicy por role
        var existingPolicies = await policyRepository.Query(new ToolPolicy());
        var policyDefinitions = new (string Role, string Allow, string Deny, bool RequireCall, int MaxTurns, int StallLimit)[]
        {
            ("build", "read,write,edit,glob,grep,shell,execute,subagent", "websearch", false, 20, 3),
            ("plan", "read,grep,glob,question,subagent", "write,edit,shell", false, 10, 3),
            ("orch", "subagent,read,grep", "write,edit,shell", true, 15, 3),
            ("moe", "read,write,edit,glob,grep,shell,subagent", "websearch", false, 20, 3),
            ("scripter", "read,write,edit,glob,grep,subagent", "shell,execute", false, 10, 3),
            ("exec", "read,write,edit,glob,grep,shell,execute", "subagent", false, 10, 3),
        };
        foreach (var (role, allow, deny, requireCall, maxTurns, stallLimit) in policyDefinitions)
        {
            var existing = existingPolicies.FirstOrDefault(policy => policy.Role == role);
            if (existing is null)
            {
                await policyRepository.Add(new ToolPolicy { Role = role, Allow = allow, Deny = deny, RequireCall = requireCall, MaxTurns = maxTurns, StallLimit = stallLimit });
                continue;
            }
            var syncedAllow = ToolListNormalizer.Normalize(existing.Allow, allow, role != "exec");
            bool delegating = role != "exec";
            var syncedDeny = existing.Deny;
            if (delegating) syncedDeny = ToolListNormalizer.RemoveToken(syncedDeny, ToolListNormalizer.SubagentToken);
            else if (!ToolListNormalizer.HasToken(syncedDeny, ToolListNormalizer.SubagentToken)) syncedDeny = ToolListNormalizer.JoinToken(syncedDeny, ToolListNormalizer.SubagentToken);
            if (syncedAllow == existing.Allow && syncedDeny == existing.Deny) continue;
            var oldPolicy = new ToolPolicy();
            foreach (var property in typeof(ToolPolicy).GetProperties()) property.SetValue(oldPolicy, property.GetValue(existing));
            existing.Allow = syncedAllow;
            existing.Deny = syncedDeny;
            await policyRepository.Update(existing, oldPolicy);
        }

        // InstructionProfile
        var existingInstructions = await instructionRepository.Query(new InstructionProfile());
        if (!existingInstructions.Any(instruction => instruction.Name == "AGENTS.md global"))
        {
            await instructionRepository.Add(new InstructionProfile { Name = "AGENTS.md global", Description = "Instrucoes globais do agente (AGENTS.md)", Version = "1.0", Status = "Approved" });
        }

        // Experiment
        var existingExperiments = await experimentRepository.Query(new Experiment());
        if (!existingExperiments.Any(experiment => experiment.Name == "OpenCodeBridge baseline"))
        {
            await experimentRepository.Add(new Experiment { Name = "OpenCodeBridge baseline", Hypothesis = "Motor TorchSharp + harness local", Status = "Planned" });
        }
    }
}
