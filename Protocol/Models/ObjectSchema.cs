using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace McpSdk.Protocol.Models;

/// <summary>
/// A JSON Schema <c>object</c> — the shape carried by a tool's <c>inputSchema</c>/<c>outputSchema</c>.
/// Each property tracks whether it is required so the <c>required</c> array round-trips. Properties may
/// themselves be objects; the root schema emits the <c>$schema</c> dialect declaration, nested ones do not.
/// </summary>
public sealed class ObjectSchema : JsonSchema, IEnumerable<KeyValuePair<string, JsonSchema>>
{
    private sealed class Property
    {
        public string Name;
        public JsonSchema Schema;
        public bool Required;
    }

    private readonly List<Property> _properties = new();
    private readonly bool _isRoot;

    public ObjectSchema()
    {
        _isRoot = true;
    }

    public ObjectSchema(IJsonObject jsonObject) : this(jsonObject, isRoot: true) {}

    private ObjectSchema(IJsonObject jsonObject, bool isRoot)
    {
        _isRoot = isRoot;

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

    /// <summary>Maps a single property object to the most specific schema it represents.</summary>
    private static JsonSchema ParseProperty(IJsonObject property)
    {
        if (property == null)
            return null;

        var type = property["type"]?.AsString();
        return type switch
        {
            "string"  => new StringSchema(property),
            "number"  => new NumberSchema(property),
            "integer" => new NumberSchema(property),
            "boolean" => new BooleanSchema(property),
            "array"   => new ArraySchema(property),
            "object"  => new ObjectSchema(property, isRoot: false),
            _         => null
        };
    }

    public ObjectSchema Add(string name, JsonSchema input)
    {
        _properties.Add(new Property { Name = name, Schema = input, Required = true });
        return this;
    }

    public ObjectSchema AddOption(string name, JsonSchema input)
    {
        _properties.Add(new Property { Name = name, Schema = input, Required = false });
        return this;
    }

    public bool IsRequired(string name) =>
        _properties.Any(p => p.Name == name && p.Required);

    public override void WriteMembers(IJsonWriter writer)
    {
        if (_isRoot)
            writer.Write("$schema", Dialect2020_12);
        writer.Write("type", "object");
        writer.Write("properties", propertyWriter =>
        {
            foreach (var property in _properties)
                propertyWriter.Write(property.Name, property.Schema);
        });
        writer.Write("required", _properties.Where(p => p.Required).Select(p => p.Name).ToArray());
    }

    public IEnumerator<KeyValuePair<string, JsonSchema>> GetEnumerator()
    {
        return _properties
            .Select(p => new KeyValuePair<string, JsonSchema>(p.Name, p.Schema))
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
