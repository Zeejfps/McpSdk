using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Json.Schema;
using McpSdk.Protocol;

namespace McpSdk.Adapter.System.Text.Json;

internal sealed class JsonElementToJsonObjectAdapter : IJsonObject
{
    private readonly JsonElement _element;

    public JsonElementToJsonObjectAdapter(JsonElement element)
    {
        _element = element;
    }

    public IJsonProperty? this[string propertyName]
    {
        get
        {
            var root = _element;
            if (root.TryGetProperty(propertyName, out var element))
            {
                return new JsonElementToJsonPropertyAdapter(element);
            }
            return null;
        }
    }

    public IEnumerator<KeyValuePair<string, IJsonProperty>> GetEnumerator()
    {
        return _element
            .EnumerateObject()
            .Select(property => new KeyValuePair<string, IJsonProperty>(property.Name, new JsonElementToJsonPropertyAdapter(property.Value)))
            .GetEnumerator();
    }

    public override string ToString()
    {
        return _element.ToString();
    }

    public bool IsValid(IJsonObject schema, out IList<string> errors)
    {
        var jsonSchema = JsonSchema.FromText(schema.ToString());
        // The default Flag output reports only overall validity with no per-node Errors, so
        // HasErrors stays false even for invalid input. Request List output and key off IsValid.
        var result = jsonSchema.Evaluate(_element, new EvaluationOptions { OutputFormat = OutputFormat.List });
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

    public void WriteMembers(IJsonWriter writer)
    {
        foreach (var kvp in this)
        {
            writer.Write(kvp.Key, kvp.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}