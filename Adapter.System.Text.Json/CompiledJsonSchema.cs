using System;
using System.Collections.Generic;
using Json.Schema;
using McpSdk.Protocol;

namespace McpSdk.Adapter.System.Text.Json;

/// <summary>
/// A JSON Schema parsed once into JsonSchema.Net's <see cref="JsonSchema"/> form, reused across
/// many <see cref="Validate"/> calls. This is where the engine dependency lives now — it used to be
/// inlined on every <see cref="JsonElementToJsonObjectAdapter"/>.
/// </summary>
internal sealed class CompiledJsonSchema : ICompiledJsonSchema
{
    private readonly JsonSchema _schema;

    public CompiledJsonSchema(JsonSchema schema)
    {
        _schema = schema;
    }

    public bool Validate(IJsonObject data, out IList<string> errors)
    {
        // This schema was compiled by the System.Text.Json adapter, so the data must be a value
        // parsed by the same adapter. Anything else is a cross-adapter mix-up — fail with a clear
        // message instead of an opaque InvalidCastException.
        if (data is not JsonElementToJsonObjectAdapter adapter)
            throw new ArgumentException(
                $"Cannot validate data of type '{data?.GetType().Name ?? "null"}' against a schema " +
                "compiled by the System.Text.Json adapter. Validate data parsed by the same IJson instance.",
                nameof(data));

        var element = adapter.Element;
        // The default Flag output reports only overall validity with no per-node Errors, so
        // HasErrors stays false even for invalid input. Request List output and key off IsValid.
        var result = _schema.Evaluate(element, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            errors = null;
            return true;
        }

        var collected = new List<string>();
        CollectErrors(result, collected);
        if (collected.Count == 0)
            collected.Add("Schema validation failed");
        errors = collected;
        return false;
    }

    private static void CollectErrors(EvaluationResults results, List<string> into)
    {
        if (results.HasErrors && results.Errors != null)
        {
            foreach (var error in results.Errors)
                into.Add($"{results.InstanceLocation}: {error.Value}");
        }

        foreach (var detail in results.Details)
            CollectErrors(detail, into);
    }
}
