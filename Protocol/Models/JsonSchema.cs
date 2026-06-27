namespace McpSdk.Protocol.Models;

public abstract class JsonSchema
{
    /// <summary>
    /// The default JSON Schema dialect for MCP as of 2025-06-18. Emitted as <c>$schema</c> on the
    /// root schema of a tool's input/output so peers parse it against the right meta-schema.
    /// </summary>
    public const string Dialect2020_12 = "https://json-schema.org/draft/2020-12/schema";

    public abstract void AsJson(IJsonWriter writer);
}