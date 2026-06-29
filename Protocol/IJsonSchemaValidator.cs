using System.Collections.Generic;

namespace McpSdk.Protocol
{
    /// <summary>
    /// Validates a JSON value against a JSON Schema. Both the value and the schema are supplied as
    /// write-only <see cref="IJsonObjectWriter"/>s, so a schema never has to be materialized as an
    /// <see cref="IJsonObject"/> merely to be validated against — it stays the write-only model it
    /// already is.
    ///
    /// This is a deliberately separate seam from <see cref="IJson"/>: schema validation is an optional,
    /// heavier concern whose backing libraries (Newtonsoft.Json.Schema, JsonSchema.Net) ship apart from
    /// the base JSON parser, so the core JSON abstraction does not depend on it. Only the server-side
    /// tool-input path needs validation; the client never does.
    /// </summary>
    public interface IJsonSchemaValidator
    {
        /// <summary>
        /// Validates <paramref name="value"/> against <paramref name="schema"/>. Returns <c>true</c> when
        /// valid; otherwise returns <c>false</c> and populates <paramref name="errors"/> with at least one
        /// message describing why.
        /// </summary>
        bool IsValid(IJsonObjectWriter value, IJsonObjectWriter schema, out IList<string> errors);
    }
}
