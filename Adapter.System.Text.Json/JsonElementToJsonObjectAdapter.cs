using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Json.Schema;
using McpSdk.Protocol;
using NotImplementedException = System.NotImplementedException;

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
        var result = jsonSchema.Evaluate(_element);
        if (result.HasErrors)
        {
            errors = result.Errors!.Values.ToArray();
            return false;
        }
        errors = null;
        return true;
    }

    public void AsJson(IJsonWriter writer)
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