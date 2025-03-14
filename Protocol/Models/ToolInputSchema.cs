using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace McpSdk.Protocol.Models;

public sealed class ToolInputSchema : IEnumerable<KeyValuePair<string, ToolInput>>
{
    private readonly Dictionary<string, ToolInput> _requiredInputsByNameLookup = new();
    private readonly Dictionary<string, ToolInput> _optionalInputsByNameLookup = new();
    
    public ToolInputSchema() {}

    public ToolInputSchema(IJsonObject jsonObject)
    {
        var properties = jsonObject["properties"]?.AsObject();
        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                var name = kvp.Key;
                var property = kvp.Value.AsObject();
                var type = property["type"].AsString();
                ToolInput input = type switch
                {
                    "string" => new StringInput(property),
                    "number" => new NumberInput(property),
                    "boolean" => new BooleanInput(property),
                    "array" => new ArrayInput(property),
                    _ => null
                };
                if (input != null)
                    _requiredInputsByNameLookup.Add(name, input);
            }
        }

    }
    
    public ToolInputSchema Add(string name, ToolInput input)
    {
        _requiredInputsByNameLookup[name] = input;
        return this;
    }

    // public ToolInputSchema AddOption(string name, ToolInput input)
    // {
    //     _optionalInputsByNameLookup[name] = input;
    //     return this;
    // }
    
    public void AsJson(IJsonWriter writer)
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

    public IEnumerator<KeyValuePair<string, ToolInput>> GetEnumerator()
    {
        return _requiredInputsByNameLookup.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}