using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace McpSdk.Protocol.Models;

/// <summary>
/// The restricted JSON Schema carried by a form-mode <c>elicitation/create</c> request. Per the
/// 2025-11-25 spec, elicitation schemas are limited to a flat object whose properties are primitives
/// (<see cref="StringSchema"/>, <see cref="NumberSchema"/>, <see cref="BooleanSchema"/>) or enums
/// (<see cref="EnumSchema"/>) — no nested objects or arrays of objects. Each property is tracked as
/// required or optional so the <c>required</c> array round-trips.
/// </summary>
public sealed class RequestedSchema : IJsonObjectWriter, IEnumerable<KeyValuePair<string, JsonSchema>>
{
    private sealed class Property
    {
        public string Name;
        public JsonSchema Schema;
        public bool Required;
    }

    private readonly List<Property> _properties = new();

    public RequestedSchema() {}

    public RequestedSchema(IJsonObject jsonObject)
    {
        var requiredNames = jsonObject["required"]?.AsStringArray();
        var required = requiredNames != null
            ? new HashSet<string>(requiredNames)
            : new HashSet<string>();

        var properties = jsonObject["properties"]?.AsObject();
        if (properties == null)
            return;

        foreach (var kvp in properties)
        {
            var schema = ParseProperty(kvp.Value.AsObject());
            if (schema != null)
                _properties.Add(new Property { Name = kvp.Key, Schema = schema, Required = required.Contains(kvp.Key) });
        }
    }

    /// <summary>Maps a single property object to the most specific restricted schema it represents.</summary>
    private static JsonSchema ParseProperty(IJsonObject property)
    {
        if (property == null)
            return null;

        var type = property["type"]?.AsString();

        // Enums: a multi-select array, a titled single-select (oneOf), or an untitled string enum.
        if (type == "array" || property["oneOf"] != null ||
            (type == "string" && property["enum"] != null))
            return new EnumSchema(property);

        // Everything else an elicitation schema allows is a scalar primitive.
        return JsonSchema.ParseScalar(property);
    }

    public RequestedSchema Add(string name, JsonSchema schema, bool required = true)
    {
        _properties.Add(new Property { Name = name, Schema = schema, Required = required });
        return this;
    }

    /// <summary>Returns the schema declared for <paramref name="name"/>, or null if absent.</summary>
    public JsonSchema this[string name] =>
        _properties.FirstOrDefault(p => p.Name == name)?.Schema;

    public bool IsRequired(string name) =>
        _properties.Any(p => p.Name == name && p.Required);

    public void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", "object");
        writer.Write("properties", properties =>
        {
            foreach (var property in _properties)
                properties.Write(property.Name, property.Schema);
        });
        writer.Write("required", _properties.Where(p => p.Required).Select(p => p.Name).ToArray());
    }

    public IEnumerator<KeyValuePair<string, JsonSchema>> GetEnumerator()
    {
        return _properties
            .Select(p => new KeyValuePair<string, JsonSchema>(p.Name, p.Schema))
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
