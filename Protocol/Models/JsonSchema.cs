namespace McpSdk.Protocol.Models;

public abstract class JsonSchema : IJsonObjectWriter
{
    /// <summary>
    /// The default JSON Schema dialect for MCP as of 2025-06-18. Emitted as <c>$schema</c> on the
    /// root schema of a tool's input/output so peers parse it against the right meta-schema.
    /// </summary>
    public const string Dialect2020_12 = "https://json-schema.org/draft/2020-12/schema";

    public abstract void WriteMembers(IJsonWriter writer);

    /// <summary>
    /// Maps a scalar property — <c>string</c>, <c>number</c>, <c>integer</c>, <c>boolean</c> — to its model,
    /// or null for anything else (composite <c>array</c>/<c>object</c> types and enum forms, which each schema
    /// dialect handles itself: <see cref="ObjectSchema"/> parses arrays and nested objects, while
    /// <see cref="RequestedSchema"/> parses enums). Centralized so the scalar set can't drift between callers —
    /// which is how <c>integer</c> once went missing from one of them.
    /// </summary>
    internal static JsonSchema ParseScalar(IJsonObject property)
    {
        if (property == null)
            return null;

        return property["type"]?.AsString() switch
        {
            "string"  => new StringSchema(property),
            "number"  => new NumberSchema(property),
            "integer" => new NumberSchema(property),
            "boolean" => new BooleanSchema(property),
            _         => null
        };
    }
}