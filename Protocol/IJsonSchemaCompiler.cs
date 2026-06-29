using System.Collections.Generic;

namespace McpSdk.Protocol
{
    /// <summary>
    /// Compiles a JSON Schema (supplied as a write-only <see cref="IJsonObjectWriter"/>, e.g. a tool's
    /// <c>inputSchema</c> model) into a reusable <see cref="IJsonSchema"/>. Compilation is the expensive
    /// step — parsing the schema text into the backing library's validator — so a caller compiles once
    /// (at tool registration) and validates many call payloads against the result.
    ///
    /// A separate seam from <see cref="IJson"/>: schema validation is an optional, heavier concern whose
    /// backing libraries (Newtonsoft.Json.Schema, JsonSchema.Net) ship apart from the base JSON parser,
    /// so the core JSON abstraction does not depend on it. Only the server-side tool-input path needs it.
    /// </summary>
    public interface IJsonSchemaCompiler
    {
        IJsonSchema Compile(IJsonObjectWriter schema);
    }

    /// <summary>
    /// A compiled, reusable JSON Schema. Validate is read-only over the schema, so a single instance may
    /// validate many values concurrently. (Distinct from <see cref="McpSdk.Protocol.Models.JsonSchema"/>,
    /// which is the write-only model that <em>describes</em> a schema; this is the compiled form that
    /// <em>checks against</em> one.)
    /// </summary>
    public interface IJsonSchema
    {
        /// <summary>
        /// Validates <paramref name="value"/> against this schema. Returns <c>true</c> when valid;
        /// otherwise returns <c>false</c> and populates <paramref name="errors"/> with at least one
        /// message describing why.
        /// </summary>
        bool Validate(IJsonObjectWriter value, out IList<string> errors);
    }
}
