using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using McpSdk.Protocol;

namespace McpSdk.Adapter.System.Text.Json;

internal sealed class JsonElementToJsonObjectAdapter : IJsonObject
{
    private readonly JsonElement _element;

    public JsonElementToJsonObjectAdapter(JsonElement element)
    {
        _element = element;
    }

    /// <summary>The underlying element, read by <see cref="CompiledJsonSchema"/> to validate this instance.</summary>
    internal JsonElement Element => _element;

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