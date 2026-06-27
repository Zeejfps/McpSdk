using System.Linq;

namespace McpSdk.Protocol.Models;

/// <summary>
/// The 2025-11-25 elicitation enum schema (SEP-1330). Models all four shapes a restricted enum can
/// take in a form-mode <c>requestedSchema</c>:
/// <list type="bullet">
/// <item>Untitled single-select — <c>{ "type":"string", "enum":[…] }</c></item>
/// <item>Titled single-select — <c>{ "type":"string", "oneOf":[{ "const":…, "title":… }] }</c></item>
/// <item>Untitled multi-select — <c>{ "type":"array", "items":{ "type":"string", "enum":[…] } }</c></item>
/// <item>Titled multi-select — <c>{ "type":"array", "items":{ "anyOf":[{ "const":…, "title":… }] } }</c></item>
/// </list>
/// <see cref="Values"/> holds the wire values (the <c>enum</c> entries or the <c>const</c>s);
/// <see cref="Titles"/>, when non-null, holds a parallel display label per value (the titled forms).
/// </summary>
public sealed class EnumSchema : JsonSchema
{
    public string Title { get; set; }
    public string Description { get; set; }

    /// <summary>When true the schema is an array (multi-select); otherwise a scalar string (single-select).</summary>
    public bool MultiSelect { get; set; }

    /// <summary>Multi-select only: minimum number of selected items.</summary>
    public int? MinItems { get; set; }

    /// <summary>Multi-select only: maximum number of selected items.</summary>
    public int? MaxItems { get; set; }

    /// <summary>The allowed wire values.</summary>
    public string[] Values { get; set; }

    /// <summary>Optional display titles, parallel to <see cref="Values"/>; null for the untitled forms.</summary>
    public string[] Titles { get; set; }

    /// <summary>
    /// Optional default selection(s). A single-select schema uses a single value (the first entry);
    /// a multi-select schema may carry several. Null when no default is declared.
    /// </summary>
    public string[] Default { get; set; }

    private bool Titled => Titles != null && Titles.Length > 0;

    public EnumSchema() {}

    public EnumSchema(IJsonObject jsonObject)
    {
        Title = jsonObject["title"]?.AsString();
        Description = jsonObject["description"]?.AsString();

        var type = jsonObject["type"]?.AsString();
        if (type == "array")
        {
            MultiSelect = true;
            MinItems = jsonObject["minItems"]?.AsInt();
            MaxItems = jsonObject["maxItems"]?.AsInt();

            var items = jsonObject["items"]?.AsObject();
            ReadChoices(items, out var values, out var titles);
            Values = values;
            Titles = titles;
            Default = jsonObject["default"]?.AsStringArray();
        }
        else
        {
            MultiSelect = false;
            ReadChoices(jsonObject, out var values, out var titles);
            Values = values;
            Titles = titles;

            var scalarDefault = jsonObject["default"]?.AsString();
            Default = scalarDefault != null ? new[] { scalarDefault } : null;
        }
    }

    private static void ReadChoices(IJsonObject from, out string[] values, out string[] titles)
    {
        values = null;
        titles = null;
        if (from == null)
            return;

        // Titled form: an array of { const, title } under oneOf (single) or anyOf (multi).
        var choices = from["oneOf"]?.AsObjectArray() ?? from["anyOf"]?.AsObjectArray();
        if (choices != null)
        {
            values = choices.Select(c => c["const"]?.AsString()).ToArray();
            titles = choices.Select(c => c["title"]?.AsString()).ToArray();
            return;
        }

        // Untitled form: a flat enum array.
        values = from["enum"]?.AsStringArray();
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        if (MultiSelect)
            WriteMultiSelect(writer);
        else
            WriteSingleSelect(writer);
    }

    private void WriteSingleSelect(IJsonWriter writer)
    {
        writer.Write("type", "string");
        if (Title != null)
            writer.Write("title", Title);
        if (Description != null)
            writer.Write("description", Description);

        if (Titled)
            writer.Write("oneOf", ChoiceWriters());
        else if (Values != null)
            writer.Write("enum", Values);

        if (Default is { Length: > 0 })
            writer.Write("default", Default[0]);
    }

    private void WriteMultiSelect(IJsonWriter writer)
    {
        writer.Write("type", "array");
        if (Title != null)
            writer.Write("title", Title);
        if (Description != null)
            writer.Write("description", Description);
        if (MinItems.HasValue)
            writer.Write("minItems", MinItems.Value);
        if (MaxItems.HasValue)
            writer.Write("maxItems", MaxItems.Value);

        writer.Write("items", items =>
        {
            if (Titled)
            {
                items.Write("anyOf", ChoiceWriters());
            }
            else
            {
                items.Write("type", "string");
                if (Values != null)
                    items.Write("enum", Values);
            }
        });

        if (Default is { Length: > 0 })
            writer.Write("default", Default);
    }

    private Json[] ChoiceWriters()
    {
        var writers = new Json[Values.Length];
        for (var i = 0; i < Values.Length; i++)
        {
            var value = Values[i];
            var title = i < Titles.Length ? Titles[i] : null;
            writers[i] = choice =>
            {
                choice.Write("const", value);
                if (title != null)
                    choice.Write("title", title);
            };
        }
        return writers;
    }
}
