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

    public JsonElementToJsonObjectAdapter(JsonElement _element)
    {
        this._element = _element;
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
}