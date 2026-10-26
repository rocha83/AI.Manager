using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// Canonicalizacao deterministica do JSON (chaves ordenadas, sem espacos,
/// cultura invariante) e checksum SHA-256 do manifesto. O hash cobre o
/// manifesto SEM o proprio campo <c>experiment.manifestHash</c>.
/// </summary>
public static class ManifestCanonicalizer
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    /// <summary>Serializa um objeto e devolve o JSON canonico (chaves ordenadas).</summary>
    public static string Canonicalize<T>(T value) =>
        Canonicalize(JsonSerializer.SerializeToNode(value, SerializerOptions));

    /// <summary>Canonicaliza um <see cref="JsonNode"/> ja existente.</summary>
    public static string Canonicalize(JsonNode? node)
    {
        var builder = new StringBuilder();
        WriteNode(builder, node);
        return builder.ToString();
    }

    /// <summary>SHA-256 do texto UTF-8, prefixado com <c>sha256:</c>.</summary>
    public static string ComputeHash(string canonicalJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Hash do manifesto com <c>experiment.manifestHash</c> removido. Remove a
    /// chave em vez de gravar null para que o hash nao dependa do preenchimento.
    /// </summary>
    public static string ComputeManifestHash(BridgeManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var node = JsonSerializer.SerializeToNode(manifest, SerializerOptions)?.AsObject()
            ?? throw new InvalidOperationException("manifesto nao serializou para objeto JSON");

        if (node["experiment"] is JsonObject experiment)
            experiment.Remove("manifestHash");

        return ComputeHash(Canonicalize(node));
    }

    /// <summary>Confere o hash informado no manifesto contra o hash calculado.</summary>
    public static bool VerifyManifestHash(BridgeManifest manifest, string? expectedHash = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var expected = string.IsNullOrWhiteSpace(expectedHash)
            ? manifest.Experiment.ManifestHash
            : expectedHash;
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return string.Equals(
            expected.Trim(),
            ComputeManifestHash(manifest),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteNode(StringBuilder builder, JsonNode? node)
    {
        switch (node)
        {
            case null:
                builder.Append("null");
                return;
            case JsonObject obj:
                WriteObject(builder, obj);
                return;
            case JsonArray array:
                WriteArray(builder, array);
                return;
            case JsonValue value:
                WriteValue(builder, value);
                return;
            default:
                throw new InvalidOperationException($"no JSON nao suportado: {node.GetType().Name}");
        }
    }

    private static void WriteObject(StringBuilder builder, JsonObject obj)
    {
        builder.Append('{');
        var first = true;
        foreach (var property in obj.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!first) builder.Append(',');
            first = false;
            builder.Append(JsonSerializer.Serialize(property.Key, SerializerOptions));
            builder.Append(':');
            WriteNode(builder, property.Value);
        }
        builder.Append('}');
    }

    private static void WriteArray(StringBuilder builder, JsonArray array)
    {
        builder.Append('[');
        for (var i = 0; i < array.Count; i++)
        {
            if (i > 0) builder.Append(',');
            WriteNode(builder, array[i]);
        }
        builder.Append(']');
    }

    private static void WriteValue(StringBuilder builder, JsonValue value)
    {
        if (value.TryGetValue<JsonElement>(out var element))
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    builder.Append(JsonSerializer.Serialize(element.GetString(), SerializerOptions));
                    return;
                case JsonValueKind.Number:
                    builder.Append(CanonicalNumber(element));
                    return;
                case JsonValueKind.True:
                    builder.Append("true");
                    return;
                case JsonValueKind.False:
                    builder.Append("false");
                    return;
                case JsonValueKind.Null:
                    builder.Append("null");
                    return;
                default:
                    builder.Append(JsonSerializer.Serialize(element.GetRawText(), SerializerOptions));
                    return;
            }
        }

        if (value.TryGetValue<string>(out var text))
        {
            builder.Append(JsonSerializer.Serialize(text, SerializerOptions));
            return;
        }
        if (value.TryGetValue<bool>(out var flag))
        {
            builder.Append(flag ? "true" : "false");
            return;
        }
        if (value.TryGetValue<double>(out var number))
        {
            builder.Append(number.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            return;
        }

        builder.Append(value.ToJsonString(SerializerOptions));
    }

    private static string CanonicalNumber(JsonElement element)
    {
        if (element.TryGetInt64(out var integer))
            return integer.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (element.TryGetDecimal(out var decimalValue))
            return decimalValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return element.GetDouble().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
    }
}
