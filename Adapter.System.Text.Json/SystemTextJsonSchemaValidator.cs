using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Json.Schema;
using McpSdk.Protocol;

namespace McpSdk.Adapter.System.Text.Json;

public sealed class SystemTextJsonSchemaValidator : IJsonSchemaValidator
{
    public bool IsValid(IJsonObjectWriter value, IJsonObjectWriter schema, out IList<string> errors)
    {
        var jsonSchema = JsonSchema.FromText(Stringify(schema));

        // Fast path: the value already holds a native JsonElement (the common case — wire-parsed
        // arguments) — evaluate it directly instead of re-serializing.
        if (value is JsonElementToJsonObjectAdapter adapter)
            return Evaluate(jsonSchema, adapter.Element, out errors);

        using var document = JsonDocument.Parse(Stringify(value));
        return Evaluate(jsonSchema, document.RootElement, out errors);
    }

    private static bool Evaluate(JsonSchema schema, JsonElement element, out IList<string> errors)
    {
        // The default Flag output reports only overall validity with no per-node Errors, so
        // HasErrors stays false even for invalid input. Request List output and key off IsValid.
        var result = schema.Evaluate(element, new EvaluationOptions { OutputFormat = OutputFormat.List });
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

    private static string Stringify(IJsonObjectWriter writer)
    {
        using var memory = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(memory))
        {
            jsonWriter.WriteStartObject();
            writer.WriteMembers(new JsonWriter(jsonWriter));
            jsonWriter.WriteEndObject();
        }
        return Encoding.UTF8.GetString(memory.ToArray());
    }
}
