using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace McpSdk.Protocol.Models;

public sealed class ObjectSchema : JsonSchema, IEnumerable<KeyValuePair<string, JsonSchema>>
{
    private readonly Dictionary<string, JsonSchema> _requiredInputsByNameLookup = new();
    private readonly Dictionary<string, JsonSchema> _optionalInputsByNameLookup = new();
    
    public ObjectSchema() {}

    public ObjectSchema(IJsonObject jsonObject)
    {
        var properties = jsonObject["properties"]?.AsObject();
        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                var name = kvp.Key;
                var property = kvp.Value.AsObject();
                var type = property["type"].AsString();
                JsonSchema input = type switch
                {
                    "string" => new StringSchema(property),
                    "number" => new NumberSchema(property),
                    "boolean" => new BooleanSchema(property),
                    "array" => new ArraySchema(property),
                    _ => null
                };
                if (input != null)
                    _requiredInputsByNameLookup.Add(name, input);
            }
        }

    }
    
    public ObjectSchema Add(string name, JsonSchema input)
    {
        _requiredInputsByNameLookup[name] = input;
        return this;
    }

    // public ToolInputSchema AddOption(string name, ToolInput input)
    // {
    //     _optionalInputsByNameLookup[name] = input;
    //     return this;
    // }
    
    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "object");
        writer.Write("properties", propertyWriter =>
        {
            foreach (var kvp in _requiredInputsByNameLookup)
            {
                var name = kvp.Key;
                var input = kvp.Value;
                propertyWriter.Write(name, input.AsJson);
            }
        });
        writer.Write("required", _requiredInputsByNameLookup.Keys.ToArray());
    }
    
    public IJsonObject AsJsonObject(IJson json)
    {
        return json.Object(AsJson);
    }

    public IEnumerator<KeyValuePair<string, JsonSchema>> GetEnumerator()
    {
        return _requiredInputsByNameLookup.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}