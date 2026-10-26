using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Bridge.Flow;
using Rochas.AI.Manager.Bridge.Transport;
using Xunit.Sdk;

namespace Rochas.AI.Manager.Bridge.Tests;

/// <summary>
/// Exercita os DADOS de teste versionados em <c>Fixtures/</c>: o manifesto de
/// smoke e o upstream falso em Python. O script e o manifesto sao a mesma fonte
/// usada no smoke manual documentado em <c>Fixtures/README.md</c>.
/// </summary>
public class FixtureDataTests
{
    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Fact]
    public void SmokeManifestValidaEGeraDryRun()
    {
        var manifest = LoadManifest("smoke.manifest.json");
        var validation = BridgeManifestValidator.Validate(manifest);

        Assert.True(
            validation.IsValid,
            string.Join("; ", validation.Errors.Select(issue => $"{issue.Code}: {issue.Message}")));

        var dryRun = ManifestCommandResolver.Resolve(manifest, new ManifestDryRunOptions
        {
            WorkspaceRoot = "/tmp/opencode",
            Model = "smoke"
        });

        Assert.True(dryRun.ManifestHashMatches);
        Assert.Equal(3, dryRun.Steps.Count);
        Assert.Contains(dryRun.Steps, step => step.Phase == "preflight");
        Assert.Contains(dryRun.Steps, step => step.CommandLine.Contains("127.0.0.1:4152"));
    }

    [Fact]
    public async Task FakeLlamaServeChatStreamEShim()
    {
        var python = FindPython();
        if (python is null) return;

        var script = Fixture("fake_llama.py");
        Assert.True(File.Exists(script), $"fixture nao copiada para o output: {script}");

        var port = FreePort();
        using var process = StartFakeLlama(python, script, port);
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await WaitReadyAsync(process, port, stderr);

            var proxy = new BridgeProxy(
                new HttpClient { Timeout = Timeout.InfiniteTimeSpan },
                new BridgeProxyOptions
                {
                    UpstreamUrl = $"http://127.0.0.1:{port}",
                    ModelId = "fake"
                });

            var plain = await proxy.CompleteAsync(Request(), new BridgeOptions());
            Assert.Equal("ok", TextOf(plain.Body));

            var events = new List<BridgeProxyEvent>();
            await foreach (var item in proxy.StreamAsync(StreamRequest(), new BridgeOptions()))
                events.Add(item);

            Assert.Contains(events, item => item.Type == "response.created");
            Assert.Contains(events, item => item.Type == "response.completed");
            var completed = events.Last(item => item.Type == "response.completed");
            Assert.Contains("ok", completed.Payload.ToJsonString());

            var shimmed = await proxy.CompleteAsync(
                ToolRequest(),
                new BridgeOptions { ToolShimMode = "json" });

            var call = shimmed.Body["output"]!.AsArray()
                .Select(node => node!.AsObject())
                .First(node => node["type"]!.GetValue<string>() == "function_call");
            Assert.Equal("read", call["name"]!.GetValue<string>());
            Assert.Contains("a.txt", call["arguments"]!.GetValue<string>());
        }
        finally
        {
            TryKill(process);
        }
    }

    private static BridgeManifest LoadManifest(string name)
    {
        var path = Fixture(name);
        Assert.True(File.Exists(path), $"fixture nao copiada para o output: {path}");
        var manifest = JsonSerializer.Deserialize<BridgeManifest>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(manifest);
        return manifest;
    }

    private static JsonObject Request() => new()
    {
        ["model"] = "m",
        ["instructions"] = "seja util",
        ["input"] = "oi"
    };

    private static JsonObject StreamRequest()
    {
        var request = Request();
        request["stream"] = true;
        return request;
    }

    private static JsonObject ToolRequest() => new()
    {
        ["model"] = "m",
        ["input"] = "leia a.txt",
        ["tools"] = new JsonArray(new JsonObject
        {
            ["type"] = "function",
            ["name"] = "read",
            ["description"] = "le arquivo",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject { ["type"] = "string" }
                },
                ["required"] = new JsonArray("path")
            }
        })
    };

    private static string TextOf(JsonObject response)
    {
        var output = response["output"]!.AsArray();
        foreach (var node in output)
        {
            if (node is not JsonObject item) continue;
            if (item["type"]?.GetValue<string>() != "message") continue;
            var content = item["content"]!.AsArray();
            if (content.Count > 0 && content[0] is JsonObject textNode)
                return textNode["text"]!.GetValue<string>();
        }
        return string.Empty;
    }

    private static string? FindPython()
    {
        foreach (var candidate in new[] { "/usr/bin/python3", "/usr/local/bin/python3" })
        {
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static Process StartFakeLlama(string python, string script, int port)
    {
        var info = new ProcessStartInfo(python)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        info.ArgumentList.Add(script);
        info.ArgumentList.Add("--port");
        info.ArgumentList.Add(port.ToString(CultureInfo.InvariantCulture));

        var process = Process.Start(info)
            ?? throw new XunitException("nao consegui iniciar o fake llama");
        _ = process.StandardOutput.ReadToEndAsync();
        return process;
    }

    private static async Task WaitReadyAsync(Process process, int port, Task<string> stderr)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (process.HasExited)
                throw new XunitException($"fake llama saiu cedo: {await stderr}");

            try
            {
                using var response = await client.GetAsync($"http://127.0.0.1:{port}/health");
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // ainda subindo
            }

            await Task.Delay(100);
        }

        throw new XunitException($"fake llama nao respondeu em 5s: {await stderr}");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
        catch
        {
            // processo ja encerrado
        }
    }
}
