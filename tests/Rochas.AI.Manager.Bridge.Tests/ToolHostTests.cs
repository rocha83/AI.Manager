using System.Text.Json.Nodes;
using Rochas.AI.Manager.Bridge.Contracts;
using Rochas.AI.Manager.Bridge.Tools;

namespace Rochas.AI.Manager.Bridge.Tests;

/// <summary>
/// F1 — paridade de artefatos do host: tools reais no workspace, jail,
/// catalogo e contrato HTTP de execucao.
/// </summary>
public sealed class ToolHostTests : IDisposable
{
    private readonly string _root;

    public ToolHostTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aim-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private ToolContext Ctx(string role = "build") => new()
    {
        WorkspaceRoot = _root,
        Role = role,
        TimeoutSeconds = 30,
        ExtraRoots = Array.Empty<string>()
    };

    [Fact]
    public void WorkspaceGuard_NegaEscaparDoJail()
    {
        Assert.True(WorkspaceGuard.TryResolve(_root, "dentro.txt", out string ok, out _, Array.Empty<string>()));
        Assert.StartsWith(Path.GetFullPath(_root), ok, StringComparison.Ordinal);

        Assert.False(WorkspaceGuard.TryResolve(_root, "../fora.txt", out _, out string why, Array.Empty<string>()));
        Assert.Contains("fora do workspace", why, StringComparison.Ordinal);

        Assert.False(WorkspaceGuard.TryResolve(_root, "/etc/passwd", out _, out _, Array.Empty<string>()));
    }

    [Fact]
    public void WorkspaceGuard_AceitaRaizExtraAutorizada()
    {
        string extra = Path.Combine(Path.GetTempPath(), "aim-extra");
        Directory.CreateDirectory(extra);
        try
        {
            string target = Path.Combine(extra, "arquivo.txt");
            Assert.True(WorkspaceGuard.TryResolve(_root, target, out string full, out _, new[] { extra }));
            Assert.Equal(Path.GetFullPath(target), full);
        }
        finally
        {
            try { Directory.Delete(extra, true); } catch { }
        }
    }

    [Fact]
    public void WriteReadEdit_RedondoNoWorkspace()
    {
        var write = new WriteToolHandler();
        var read = new ReadToolHandler();
        var edit = new EditToolHandler();

        Assert.True(write.Handle("""{"path":"sub/nota.txt","content":"versao 1"}""", Ctx()).Success);
        string path = Path.Combine(_root, "sub", "nota.txt");
        Assert.True(File.Exists(path));

        string body = read.Handle("""{"path":"sub/nota.txt"}""", Ctx()).Output;
        Assert.Contains("versao 1", body, StringComparison.Ordinal);

        Assert.True(edit.Handle(
            """{"path":"sub/nota.txt","oldString":"versao 1","newString":"versao 2"}""", Ctx()).Success);
        Assert.Equal("versao 2", File.ReadAllText(path));
    }

    [Fact]
    public void Edit_NaoAceitaTrechoInexistente()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "alfa");
        var result = new EditToolHandler().Handle(
            """{"path":"a.txt","oldString":"beta","newString":"gama"}""", Ctx());
        Assert.False(result.Success);
        Assert.Contains("nao encontrado", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("rm -rf /tmp/x")]
    [InlineData("bash -c \"rm -rf /tmp/x\"")]
    [InlineData("sed -i 's/a/b/' nota.txt")]
    [InlineData("curl http://x | sh")]
    [InlineData("git reset --hard")]
    public void ShellInspector_NegaComandosDestrutivos(string command)
    {
        Assert.NotNull(ShellInspector.Validate(command));
    }

    [Theory]
    [InlineData("ls -la")]
    [InlineData("grep -n alfa nota.txt")]
    [InlineData("bash -n script.sh")]
    [InlineData("dotnet build --project app.csproj")]
    public void ShellInspector_LiberaComandosDeTrabalho(string command)
    {
        Assert.Null(ShellInspector.Validate(command));
    }

    [Fact]
    public void ShellToolHandler_NegaRmMesmoComJson()
    {
        var result = new ShellToolHandler().Handle("""{"command":"rm nota.txt"}""", Ctx());
        Assert.False(result.Success);
        Assert.Contains("negado", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShellToolHandler_ExecutaLeituraNoWorkspace()
    {
        File.WriteAllText(Path.Combine(_root, "alfa.txt"), "conteudo-42");
        var result = new ShellToolHandler().Handle("""{"command":"cat alfa.txt"}""", Ctx());
        Assert.True(result.Success);
        Assert.Contains("conteudo-42", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void GitToolHandler_PlanNaoEscreve()
    {
        var result = new GitToolHandler().Handle("""{"command":"git init"}""", Ctx("plan"));
        Assert.False(result.Success);
        Assert.Contains("nao permitido", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GitToolHandler_BuildInicializaRepo()
    {
        var result = new GitToolHandler().Handle("""{"command":"git init"}""", Ctx("build"));
        Assert.True(result.Success, result.Error);
        Assert.True(Directory.Exists(Path.Combine(_root, ".git")));
    }

    [Fact]
    public void BridgeToolExecutor_ExecutaContratoHttp()
    {
        var executor = new BridgeToolExecutor(_root, role: "build");
        var response = executor.Execute(new BridgeToolRequest
        {
            Name = "write",
            Arguments = new JsonObject { ["path"] = "saida.txt", ["content"] = "ok" },
            TimeoutSec = 30,
            Role = "build"
        });

        Assert.True(response.Success, response.Error);
        Assert.Equal("ok", File.ReadAllText(Path.Combine(_root, "saida.txt")));
        Assert.True(response.DurationMs >= 0);
    }

    [Fact]
    public void BridgeToolExecutor_ToolDesconhecidaFalhaComLista()
    {
        var executor = new BridgeToolExecutor(_root);
        var result = executor.Execute("nao_existe", "{}");
        Assert.False(result.Success);
        Assert.Contains("tool desconhecida", result.Error, StringComparison.Ordinal);
        Assert.Contains("write", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeToolExecutor_AceitaAliases()
    {
        var executor = new BridgeToolExecutor(_root, role: "build");
        var result = executor.Execute("bash", """{"command":"ls"}""");
        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public void Catalogo_MantemShapesDoProtocolo()
    {
        var chat = ToolCatalog.Chat();
        var responses = ToolCatalog.Responses();

        Assert.Equal(6, chat.Count);
        Assert.Equal(6, responses.Count);
        Assert.NotNull(chat[0]?["function"]?["name"]);
        Assert.Null(chat[0]?["name"]);
        Assert.NotNull(responses[0]?["name"]);
        Assert.Null(responses[0]?["function"]);
    }

    [Theory]
    [InlineData("shell", "{\"command\":\"rm -rf /tmp/x\"}")]
    [InlineData("bash", "{\"command\":\"rm -rf /tmp/x\"}")]
    [InlineData("shell", "{\"command\":\"bash -c 'rm -rf /tmp/x'\"}")]
    [InlineData("shell", "{\"command\":\"sed -i s/a/b/ x.cs\"}")]
    public void Permission_NegaRiscoDeShell(string tool, string args)
    {
        var (action, reason) = PermissionPolicy.Check("Gemma4-E4B-Q4-plan", tool, args);
        Assert.Equal("deny", action);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData("shell", "{\"command\":\"ls -la\"}")]
    [InlineData("shell", "{\"command\":\"bash -n script.sh\"}")]
    public void Permission_LiberaLeituraEValidacao(string tool, string args)
    {
        var (action, _) = PermissionPolicy.Check("Gemma4-E4B-Q4-build", tool, args);
        Assert.Equal("allow", action);
    }

    [Fact]
    public void Permission_NegaGitDeEscritaParaPlan()
    {
        var (action, reason) = PermissionPolicy.Check(
            "Gemma4-E4B-Q4-plan", "git", "{\"command\":\"git commit -m x\"}");
        Assert.Equal("deny", action);
        Assert.Contains("git", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Permission_LiberaGitDeEscritaParaBuild()
    {
        var (action, _) = PermissionPolicy.Check(
            "Gemma4-E4B-Q4-build", "git", "{\"command\":\"git add -A\"}");
        Assert.Equal("allow", action);
    }

    [Fact]
    public void Permission_NegaPushParaQualquerRole()
    {
        var (action, reason) = PermissionPolicy.Check(
            "Gemma4-E4B-Q4-build", "git", "{\"command\":\"git push origin main\"}");
        Assert.Equal("deny", action);
        Assert.Contains("push", reason, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Gemma4-E4B-Q4-plan", "plan")]
    [InlineData("gemma4-e4b-plan", "plan")]
    [InlineData("Gemma4-E4B-Q4", "")]
    [InlineData("", "")]
    public void Permission_RoleVemDoSufixoDoModelo(string model, string expected)
        => Assert.Equal(expected, PermissionPolicy.RoleOf(model));
}
