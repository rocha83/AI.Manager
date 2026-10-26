using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Tools;

/// <summary>
/// Catalogo canonico das tools de sistema do host. E a fonte unica do "que o
/// modelo pode chamar": a Web, o runner e os testes repassam este array; nao
/// inventam tool nem reescrevem schema.
/// </summary>
/// <remarks>
/// Dois shapes, os mesmos do protocolo:
/// <list type="bullet">
/// <item><see cref="Chat"/> — <c>chat/completions</c>: <c>{type:function, function:{name,description,parameters}}</c>.</item>
/// <item><see cref="Responses"/> — <c>/v1/responses</c>: <c>{type:function, name, description, parameters}</c>.</item>
/// </list>
/// Mandar o shape chat no endpoint Responses faz a call de volta nao ser
/// reconhecida e o turno volta vazio.
/// </remarks>
public static class ToolCatalog
{
    /// <summary>Tools no shape <c>chat/completions</c>.</summary>
    public static JsonArray Chat() => new()
    {
        Fn("shell", "Executa um comando de console. Um comando por chamada; sem heredoc.", new JsonObject
        {
            ["command"] = new JsonObject { ["type"] = "string" }
        }, "command"),
        Fn("write", "Cria/sobrescreve um arquivo de texto.", new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string" },
            ["content"] = new JsonObject { ["type"] = "string" }
        }, "path", "content"),
        Fn("read", "Le um arquivo de texto.", new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string" }
        }, "path"),
        Fn("edit", "Substitui um trecho exato de um arquivo.", new JsonObject
        {
            ["path"] = new JsonObject { ["type"] = "string" },
            ["oldString"] = new JsonObject { ["type"] = "string" },
            ["newString"] = new JsonObject { ["type"] = "string" }
        }, "path", "oldString", "newString"),
        Fn("glob", "Lista arquivos por padrao. Nao aceita alternancia com |.", new JsonObject
        {
            ["pattern"] = new JsonObject { ["type"] = "string" }
        }, "pattern"),
        Fn("grep", "Busca expressao regular no conteudo dos arquivos.", new JsonObject
        {
            ["pattern"] = new JsonObject { ["type"] = "string" }
        }, "pattern")
    };

    /// <summary>Tools no shape <c>/v1/responses</c> (a partir de <see cref="Chat"/>).</summary>
    public static JsonArray Responses()
    {
        var arr = new JsonArray();
        foreach (var t in Chat())
        {
            if (t?["function"] is JsonObject fn)
                arr.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["name"] = fn["name"]?.DeepClone(),
                    ["description"] = fn["description"]?.DeepClone(),
                    ["parameters"] = fn["parameters"]?.DeepClone()
                });
            else if (t is not null) arr.Add(t.DeepClone());
        }
        return arr;
    }

    private static JsonObject Fn(string name, string description, JsonObject props, params string[] required)
    {
        var req = new JsonArray();
        foreach (string r in required) req.Add(r);
        return new JsonObject
        {
            ["type"] = "function",
            ["function"] = new JsonObject
            {
                ["name"] = name,
                ["description"] = description,
                ["parameters"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = req
                }
            }
        };
    }
}
