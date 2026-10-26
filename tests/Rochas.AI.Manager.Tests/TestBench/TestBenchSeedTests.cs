using Rochas.AI.Manager.Application.TestBench;
using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Domain.Experiments;
using Rochas.AI.Manager.Infrastructure.Data;
using Rochas.AI.Manager.Infrastructure.TestBench;

namespace Rochas.AI.Manager.Tests.TestBench;

/// <summary>
/// F2 — enunciados como dados: baseline versionado, seed idempotente, hash
/// canonico e projecao para o runner de gate (Q4) e espelho (Q3).
/// </summary>
public sealed class TestBenchSeedTests
{
    private static string SeedPath()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Seeds", "war-baseline.json");
        Assert.True(File.Exists(path), $"seed nao copiado para a saida: {path}");
        return path;
    }

    private static string TempDb()
        => Path.Combine(Path.GetTempPath(), $"aim-testbench-{Guid.NewGuid():N}.db");

    [Fact]
    public void Seed_ValidaManifestoECalculaHash()
    {
        var baseline = WarBaselineReader.Load(SeedPath());

        Assert.Equal("war-baseline-2026-10-26", baseline.SourceCommit);
        Assert.Equal(2, baseline.Manifest.Scenarios.Count);
        Assert.Equal(2, baseline.Runners.Count);
        Assert.False(string.IsNullOrWhiteSpace(baseline.Manifest.Experiment.ManifestHash));

        string recomputed = ManifestCanonicalizer.ComputeManifestHash(baseline.Manifest);
        Assert.Equal(recomputed, baseline.Manifest.Experiment.ManifestHash);
    }

    [Fact]
    public void Seed_EIdempotentePorCommitEHash()
    {
        string db = TempDb();
        try
        {
            var first = TestBenchSeeder.SeedFile(SeedPath(), db);
            Assert.True(first.Inserted);
            Assert.True(first.PlanId > 0);
            Assert.Equal(2, first.Scenarios);
            Assert.Equal(2, first.Runners);

            var second = TestBenchSeeder.SeedFile(SeedPath(), db);
            Assert.False(second.Inserted);
            Assert.Equal(first.PlanId, second.PlanId);
            Assert.Equal(first.ManifestHash, second.ManifestHash);
            Assert.Equal(0, second.Scenarios);

            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(AppDb.EnsureDatabase(db));
            connection.Open();
            using var count = connection.CreateCommand();
            count.CommandText = """
                SELECT (SELECT COUNT(*) FROM experiment_plans),
                       (SELECT COUNT(*) FROM circuit_stages),
                       (SELECT COUNT(*) FROM circuit_steps),
                       (SELECT COUNT(*) FROM test_scenarios),
                       (SELECT COUNT(*) FROM runner_profiles);
                """;
            using var reader = count.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(1, reader.GetInt64(0));
            Assert.Equal(1, reader.GetInt64(1));
            Assert.Equal(2, reader.GetInt64(2));
            Assert.Equal(2, reader.GetInt64(3));
            Assert.Equal(2, reader.GetInt64(4));
        }
        finally
        {
            TryDelete(db);
        }
    }

    [Fact]
    public void Seed_NaoSobrescrevePlanoAprovado()
    {
        string db = TempDb();
        try
        {
            var seed = TestBenchSeeder.SeedFile(SeedPath(), db);
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(AppDb.EnsureDatabase(db)))
            {
                connection.Open();
                using var approve = connection.CreateCommand();
                approve.CommandText = "UPDATE experiment_plans SET status = 'Approved' WHERE id = $id";
                approve.Parameters.AddWithValue("$id", seed.PlanId);
                approve.ExecuteNonQuery();
            }

            var again = TestBenchSeeder.SeedFile(SeedPath(), db);
            Assert.False(again.Inserted);

            using var check = new Microsoft.Data.Sqlite.SqliteConnection(AppDb.EnsureDatabase(db));
            check.Open();
            using var status = check.CreateCommand();
            status.CommandText = "SELECT status, manifest_json FROM experiment_plans WHERE id = $id";
            status.Parameters.AddWithValue("$id", seed.PlanId);
            using var reader = status.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal("Approved", reader.GetString(0));
            Assert.Contains("azul-build", reader.GetString(1), StringComparison.Ordinal);
        }
        finally
        {
            TryDelete(db);
        }
    }

    [Fact]
    public void ManifestBuilder_BuildForRunnerTrocaTargetEMantemCenarios()
    {
        var baseline = WarBaselineReader.Load(SeedPath());

        var q4 = ManifestBuilder.BuildForRunner(baseline, "q4");
        Assert.True(q4.Target.IsGate);
        Assert.Equal("http://127.0.0.1:4124", q4.Target.BridgeUrl);
        Assert.Equal(2, q4.Scenarios.Count);

        var q3 = ManifestBuilder.BuildForRunner(baseline, "q3");
        Assert.False(q3.Target.IsGate);
        Assert.Equal("http://127.0.0.1:4125", q3.Target.BridgeUrl);
        Assert.Equal(114688, q3.Target.ContextWindow);
        Assert.Equal(q4.Scenarios.Select(s => s.Key), q3.Scenarios.Select(s => s.Key));
        Assert.NotEqual(q4.Experiment.ManifestHash, q3.Experiment.ManifestHash);
    }

    [Fact]
    public void ManifestBuilder_RejeitaHashDivergente()
    {
        var baseline = WarBaselineReader.Load(SeedPath());
        baseline.Manifest.Experiment.ManifestHash = new string('0', 64);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ManifestBuilder.Normalize(baseline.Manifest));
        Assert.Contains("manifestHash nao confere", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Codec_RejeitaManifestoInvalido()
    {
        const string json = """
            {
              "sourceCommit": "x",
              "manifest": {
                "experiment": { "name": "" },
                "runner": { "id": "", "capabilities": [] },
                "target": { "bridgeUrl": "nao-e-url", "upstreamUrl": "" },
                "stages": [],
                "scenarios": []
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(() => BridgeBaselineCodec.Parse(json));
        Assert.Contains("manifesto invalido", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ExperimentoDeTesteDeclaraKindEEnunciado()
    {
        var experiment = new Experiment
        {
            Name = "WAR L1",
            Kind = "Test",
            Statement = "belts build como dados",
        };

        Assert.Equal("Test", experiment.Kind);
        Assert.Equal("belts build como dados", experiment.Statement);
    }

    [Fact]
    public void AppDb_CriaTabelasDoTestBench()
    {
        string db = TempDb();
        try
        {
            string connectionString = AppDb.EnsureDatabase(db);
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                SELECT name FROM sqlite_master
                 WHERE type = 'table'
                   AND name IN ('experiment_plans','circuit_stages','circuit_steps',
                                'test_scenarios','runner_profiles','test_runs',
                                'test_stage_runs','test_step_runs','test_scenario_runs',
                                'test_evidence');
                """;
            var names = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) names.Add(reader.GetString(0));
            Assert.Equal(10, names.Count);
        }
        finally
        {
            TryDelete(db);
        }
    }

    private static void TryDelete(string db)
    {
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(db + suffix); } catch { }
        }
    }
}
