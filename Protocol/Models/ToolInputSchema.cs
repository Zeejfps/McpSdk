using System;
using System.Collections;
using System.Collections.Generic;

namespace McpSdk.Protocol.Models;

public sealed class ToolInputSchema : IEnumerable<KeyValuePair<string, ToolInput>>
{
    private readonly Dictionary<string, ToolInput> _inputsByNameLookup = new();
    
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
                    _ => null
                };
                if (input != null)
                    _inputsByNameLookup.Add(name, input);
            }
        }

    }
    
    public ToolInputSchema Add(string name, ToolInput writeInput)
    {
        _inputsByNameLookup[name] = writeInput;
        return this;
    }

    public void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "object");
        writer.Write("properties", propertyWriter =>
        {
            foreach (var kvp in _inputsByNameLookup)
            {
                var name = kvp.Key;
                var input = kvp.Value;
                propertyWriter.Write(name, input.AsJson);
            }
        });
    }
    
    public IJsonObject AsJsonObject(IJson json)
    {
        return json.Object(AsJson);
    }

    public IEnumerator<KeyValuePair<string, ToolInput>> GetEnumerator()
    {
        return _inputsByNameLookup.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}