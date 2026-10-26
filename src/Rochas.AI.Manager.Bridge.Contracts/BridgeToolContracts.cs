using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Contrato de execucao de tool exposto pelo host. As tools são registradas em
/// codigo no Bridge; o manifesto so referencia nomes/argumentos conhecidos.
/// </summary>
public sealed class BridgeToolRequest
{
    public string Name { get; set; } = string.Empty;
    public JsonObject Arguments { get; set; } = new();
    public string? WorkspaceRoot { get; set; }
    public int TimeoutSec { get; set; }
    public string? Role { get; set; }
}

public sealed class BridgeToolResponse
{
    public bool Success { get; set; }
    public string? Output { get; set; }
    public string? Error { get; set; }
    public int? ExitCode { get; set; }
    public long DurationMs { get; set; }
    public JsonObject? Metadata { get; set; }
}

/// <summary>Erro tipado do cliente HTTP/SSE do Bridge.</summary>
public sealed class BridgeClientException : Exception
{
    public BridgeClientException(string message, int? statusCode = null, string? responseBody = null, string? code = null)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Code = code;
    }

    public int? StatusCode { get; }
    public string? ResponseBody { get; }
    public string? Code { get; }
}

/// <summary>Evento SSE ja parseado (linha <c>event:</c> + bloco <c>data:</c>).</summary>
public sealed record BridgeSseEvent(string? EventName, string Data);
