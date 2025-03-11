using System.Text.Json;
using McpSharp.Protocol;

namespace McpSharp.Adapter.System.Text.Json;

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
}