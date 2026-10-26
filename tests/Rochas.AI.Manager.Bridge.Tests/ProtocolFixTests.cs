using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ProtocolFixTests
{
    private static BridgeOptions Plain() => new()
    {
        Harness = false,
        MainHarness = false,
        ToolStrategy = false,
        BreakerSoft = 0,
        BreakerHard = 0,
        CoerceNarrated = false
    };

    [Fact]
    public void FunctionCallsConsecutivasViramUmTurno()
    {
        var request = new JsonObject
        {
            ["instructions"] = "sys",
            ["input"] = new JsonArray(
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["call_id"] = "c1",
                    ["name"] = "read",
                    ["arguments"] = """{"path":"a.txt"}"""
                },
                new JsonObject
                {
                    ["type"] = "function_call",
                    ["call_id"] = "c2",
                    ["name"] = "read",
                    ["arguments"] = """{"path":"b.txt"}"""
                },
                new JsonObject { ["type"] = "function_call_output", ["call_id"] = "c1", ["output"] = "A" },
                new JsonObject { ["type"] = "function_call_output", ["call_id"] = "c2", ["output"] = "B" })
        };

        var result = ResponsesToChat.Convert(request, Plain(), "m");
        var messages = result.Body["messages"]!.AsArray();

        Assert.Equal(4, messages.Count);
        Assert.Equal("assistant", messages[1]!["role"]!.GetValue<string>());
        Assert.Equal(2, messages[1]!["tool_calls"]!.AsArray().Count);
        Assert.Equal("c1", messages[1]!["tool_calls"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("c2", messages[1]!["tool_calls"]![1]!["id"]!.GetValue<string>());
    }

    [Fact]
    public void CallNarradaSaiDoHistoricoNoHarnessLegado()
    {
        var request = new JsonObject
        {
            ["instructions"] = "sys",
            ["input"] = new JsonArray(new JsonObject
            {
                ["type"] = "message",
                ["role"] = "assistant",
                ["content"] = "[Assistant tool call]: read({\"path\":\"a.txt\"}) pronto"
            })
        };
        var options = Plain();
        options.LegacyHarness = true;

        var result = ResponsesToChat.Convert(request, options, "m");
        string content = result.Body["messages"]![1]!["content"]!.GetValue<string>();

        Assert.Equal("pronto", content.Trim());
        Assert.DoesNotContain("[Assistant tool call]", content);
    }

    [Fact]
    public void ToolExecuteSaiDoCatalogo()
    {
        var request = new JsonObject
        {
            ["input"] = "oi",
            ["tools"] = new JsonArray(
                new JsonObject { ["type"] = "function", ["name"] = "execute", ["parameters"] = new JsonObject() },
                new JsonObject
                {
                    ["type"] = "function",
                    ["name"] = "read",
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject { ["path"] = new JsonObject { ["type"] = "string" } }
                    }
                })
        };
        var options = Plain();
        options.ToolStrategy = true;

        var result = ResponsesToChat.Convert(request, options, "m");
        var tools = result.Body["tools"]!.AsArray();

        Assert.Single(tools);
        Assert.Equal("read", tools[0]!["function"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void PatternNaoAncoradoSaiDoSchema()
    {
        var request = new JsonObject
        {
            ["input"] = "oi",
            ["tools"] = new JsonArray(new JsonObject
            {
                ["type"] = "function",
                ["name"] = "read",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["path"] = new JsonObject { ["type"] = "string", ["pattern"] = "a.*b" }
                    }
                }
            })
        };
        var options = Plain();
        options.ToolStrategy = true;

        var result = ResponsesToChat.Convert(request, options, "m");

        var path = result.Body["tools"]![0]!["function"]!["parameters"]!["properties"]!["path"]!.AsObject();
        Assert.False(path.ContainsKey("pattern"));
        Assert.Contains("$[0].function.parameters.properties.path.pattern", result.PatternHits);
    }

    [Fact]
    public void BashMantemONomeAnunciadoPeloCliente()
    {
        var chat = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["content"] = null,
                    ["tool_calls"] = new JsonArray(new JsonObject
                    {
                        ["id"] = "call_1",
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = "shell",
                            ["arguments"] = """{"command":"ls"}"""
                        }
                    })
                }
            })
        };

        var response = ChatToResponses.Convert(
            chat, "m", 0.1, Plain(), hadTools: true, announcedTools: new[] { "bash" });

        Assert.Equal("bash", response["output"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void ToolCoercionLePseudoJsDoFormatoNarrado()
    {
        var calls = ToolCoercion.ExtractTextToolCalls(
            "[Assistant tool call]: shell({command: 'ls -la'})",
            new List<string> { "shell" });

        var only = Assert.Single(calls);
        Assert.Equal("shell", only.Name);
        Assert.Equal("ls -la", JsonNode.Parse(only.Arguments)!["command"]!.GetValue<string>());
    }

    [Fact]
    public void QwenStandardTraduzToolsEArgumentosNosDoisSentidos()
    {
        var tools = new JsonArray(new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = "read",
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject { ["path"] = new JsonObject { ["type"] = "string" } },
                    ["required"] = new JsonArray("path")
                }
            }
        });

        QwenStandard.TranslateToolsForUpstream(tools);

        var function = tools[0]!["function"]!;
        Assert.Equal("read_file", function["name"]!.GetValue<string>());
        Assert.NotNull(function["parameters"]!["properties"]!["file_path"]);
        Assert.Equal("file_path", function["parameters"]!["required"]![0]!.GetValue<string>());

        var chat = new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["message"] = new JsonObject
                {
                    ["tool_calls"] = new JsonArray(new JsonObject
                    {
                        ["function"] = new JsonObject
                        {
                            ["name"] = "read_file",
                            ["arguments"] = """{"file_path":"a.txt"}"""
                        }
                    })
                }
            })
        };
        QwenStandard.TranslateCompletionForClient(chat);

        var call = chat["choices"]![0]!["message"]!["tool_calls"]![0]!["function"]!;
        Assert.Equal("read", call["name"]!.GetValue<string>());
        Assert.Equal("a.txt", JsonNode.Parse(call["arguments"]!.GetValue<string>())!["path"]!.GetValue<string>());
    }

    [Fact]
    public void MainHarnessSubstituiSystemEPreservaBlocosDinamicos()
    {
        var messages = new JsonArray(new JsonObject
        {
            ["role"] = "system",
            ["content"] = "<env>ENV-REAL</env> <available_skills>SKILL-X</available_skills> instrucao pequena"
        });

        Assert.True(MainHarness.Apply(messages, enabled: true));
        string system = messages[0]!["content"]!.GetValue<string>();

        Assert.StartsWith(MainHarness.Text, system);
        Assert.Contains("<env>ENV-REAL</env>", system);
        Assert.Contains("<available_skills>SKILL-X</available_skills>", system);
        Assert.Contains("INSTRUCOES DO CLIENTE", system);
    }

    [Fact]
    public void MainHarnessNaoTrocaOMasterDoOpencode()
    {
        var messages = new JsonArray(new JsonObject
        {
            ["role"] = "system",
            ["content"] = "# Code Mode\ninstrucoes do opencode"
        });

        Assert.False(MainHarness.Apply(messages, enabled: true));
        Assert.Equal("# Code Mode\ninstrucoes do opencode",
            messages[0]!["content"]!.GetValue<string>());
    }

    [Fact]
    public void MainHarnessCoexisteComAsCaudas()
    {
        var request = new JsonObject
        {
            ["instructions"] = "system do cliente",
            ["input"] = "oi"
        };
        var options = Plain();
        options.Harness = true;
        options.MainHarness = true;
        options.BehaviorTail = "BEHAVIOR-TAIL";
        options.ToolsTail = "TOOLS-TAIL";

        var result = ResponsesToChat.Convert(request, options, "m");
        string system = result.Body["messages"]![0]!["content"]!.GetValue<string>();

        Assert.StartsWith(MainHarness.Text, system);
        Assert.Contains("BEHAVIOR-TAIL", system);
        Assert.Contains("TOOLS-TAIL", system);
    }

    [Fact]
    public void MainHarnessNaoBloqueiaPorSufixoDeRole()
    {
        var request = new JsonObject { ["input"] = "oi" };
        var options = Plain();
        options.MainHarness = true;
        options.HasProfile = true;

        var result = ResponsesToChat.Convert(request, options, "m");
        string system = result.Body["messages"]![0]!["content"]!.GetValue<string>();

        Assert.StartsWith(MainHarness.Text, system);
    }

    [Fact]
    public void MainHarnessNaoAplicaNoCliOpencode()
    {
        var request = new JsonObject { ["input"] = "oi" };
        var options = Plain();
        options.MainHarness = true;
        options.OpencodeClient = true;
        options.BehaviorTail = "BEHAVIOR-TAIL";

        var result = ResponsesToChat.Convert(request, options, "m");
        string system = result.Body["messages"]![0]!["content"]!.GetValue<string>();

        // Base e tails ficam de fora no escopo "managed" para o CLI do opencode.
        Assert.False(system.StartsWith(MainHarness.Text));
        Assert.DoesNotContain("BEHAVIOR-TAIL", system);
    }

    [Fact]
    public void HarnessTailsComposeMantemOrdem()
    {
        string text = HarnessTails.Compose("sys", true, "B", "T");

        Assert.Equal("sys\n\nB\n\nT", text);
        Assert.Equal("sys", HarnessTails.Compose("sys", false, "B", "T"));
    }

    [Fact]
    public void MaxOutputTokensDoClienteVenceOTetoDoMotor()
    {
        var request = new JsonObject
        {
            ["input"] = "oi",
            ["max_output_tokens"] = 77
        };

        var result = ResponsesToChat.Convert(request, new BridgeOptions { MaxTokens = 10 }, "m");

        Assert.Equal(77, result.Body["max_tokens"]!.GetValue<int>());
    }

    [Fact]
    public void SamplingPassthroughCopiaParametros()
    {
        var request = new JsonObject
        {
            ["input"] = "oi",
            ["top_p"] = 0.9,
            ["repeat_penalty"] = 1.1
        };
        var options = new BridgeOptions { Harness = false, SamplingPassthrough = true };

        var result = ResponsesToChat.Convert(request, options, "m");

        Assert.Equal(0.9, result.Body["top_p"]!.GetValue<double>());
        Assert.Equal(1.1, result.Body["repeat_penalty"]!.GetValue<double>());
    }
}
