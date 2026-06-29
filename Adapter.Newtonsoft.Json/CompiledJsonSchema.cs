using System;
using System.Collections.Generic;
using McpSdk.Protocol;
using Newtonsoft.Json.Schema;

namespace McpSdk.Adapter.Newtonsoft.Json
{
    /// <summary>
    /// A JSON Schema parsed once into Newtonsoft's <see cref="JSchema"/> form, reused across many
    /// <see cref="Validate"/> calls. The engine dependency lives here now rather than on every
    /// <see cref="JTokenToJsonObjectAdapter"/>.
    /// </summary>
    internal sealed class CompiledJsonSchema : ICompiledJsonSchema
    {
        private readonly JSchema _schema;

        public CompiledJsonSchema(JSchema schema)
        {
            _schema = schema;
        }

        public bool Validate(IJsonObject data, out IList<string> errors)
        {
            if (data is not JTokenToJsonObjectAdapter adapter)
                throw new ArgumentException(
                    $"Cannot validate data of type '{data?.GetType().Name ?? "null"}' against a schema " +
                    "compiled by the Newtonsoft.Json adapter. Validate data parsed by the same IJson instance.",
                    nameof(data));

            return adapter.Token.IsValid(_schema, out errors);
        }
    }
}
