using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tests;

public class ResponsesStreamParityTests
{
    [Fact]
    public void ToolArgsEmLoopInterrompeEnaoEmiteCallParcial()
    {
        var events = new List<(string Type, JsonObject Payload)>();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions
            {
                AntiRepeat = true,
                AntiRepeatWindow = 256,
                Thinking = "events",
                CoerceNarrated = false
            },
            (type, payload) => events.Add((type, payload)),
            "m", 0.1, hadTools: true);

        translator.Start();
        string repeated = string.Concat(Enumerable.Repeat(new string('x', 1200), 3));
        translator.Feed(new JsonObject
        {
            ["choices"] = new JsonArray(new JsonObject
            {
                ["delta"] = new JsonObject
                {
                    ["tool_calls"] = new JsonArray(new JsonObject
                    {
                        ["index"] = 0,
                        ["id"] = "call_1",
                        ["function"] = new JsonObject
                        {
                            ["name"] = "write",
                            ["arguments"] = repeated
                        }
                    })
                }
            })
        });

        Assert.True(translator.RepeatTripped);
        Assert.Equal("tool-args", translator.RepeatWhere);

        var final = translator.Finish();
        var output = final["output"]!.AsArray();

        Assert.DoesNotContain(output, node => node?["type"]?.GetValue<string>() == "function_call");
        Assert.Contains(output, node =>
            node?["type"]?.GetValue<string>() == "message"
            && node["content"]![0]!["text"]!.GetValue<string>().Contains("contencao"));
    }

    [Fact]
    public void ConteudoEmLoopInterrompeOStream()
    {
        var events = new List<(string Type, JsonObject Payload)>();
        var translator = new ResponsesStreamTranslator(
            new BridgeOptions { AntiRepeat = true, AntiRepeatWindow = 64, Thinking = "events" },
            (type, payload) => events.Add((type, payload)),
            "m", 0.1);

        translator.Start();
        string block = "o mesmo raciocinio repetido varias vezes. ";
        for (int i = 0; i < 6; i++)
        {
            translator.Feed(new JsonObject
            {
                ["choices"] = new JsonArray(new JsonObject
                {
                    ["delta"] = new JsonObject { ["content"] = block }
                })
            });
            if (translator.RepeatTripped) break;
        }

        Assert.True(translator.RepeatTripped);
        Assert.Equal("content", translator.RepeatWhere);
    }
}
