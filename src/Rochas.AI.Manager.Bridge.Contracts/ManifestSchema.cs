using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Rochas.AI.Manager.Bridge.Contracts;

/// <summary>
/// JSON Schema do manifesto, gerado do proprio DTO. O cliente pode buscar em
/// <c>/api/contract/schema</c> e validar no editor sem conhecer o C#.
/// </summary>
public static class ManifestSchema
{
    private static readonly JsonSerializerOptions SchemaOptions = new(ManifestCanonicalizer.SerializerOptions)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static JsonNode Generate() =>
        JsonSchemaExporter.GetJsonSchemaAsNode(
            SchemaOptions,
            typeof(BridgeManifest),
            new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true });

    public static string GenerateJson() =>
        Generate().ToJsonString(ManifestCanonicalizer.SerializerOptions);
}
