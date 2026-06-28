namespace McpSdk.Protocol.Models;

public sealed class Resource : IJsonObjectWriter
{
    public string Uri { get; }
    public string Name { get; }

    /// <summary>Human-friendly display title (2025-06-18); falls back to Name when absent.</summary>
    public string Title { get; }

    public string Description { get; }
    public string MimeType { get; }

    /// <summary>Optional display icons (2025-11-25).</summary>
    public Icon[] Icons { get; }

    /// <summary>Opaque, implementation-defined metadata.</summary>
    public Meta Meta { get; }

    public Resource(string uri, string name, string description = null, string mimeType = null,
        string title = null, Icon[] icons = null, Meta meta = null)
    {
        Uri = uri;
        Name = name;
        Title = title;
        Description = description;
        MimeType = mimeType;
        Icons = icons;
        Meta = meta;
    }

    public Resource(IJsonObject jsonObject)
    {
        Uri = jsonObject["uri"].AsString();
        Name = jsonObject["name"].AsString();
        Title = jsonObject["title"]?.AsString();
        Description = jsonObject["description"]?.AsString();
        MimeType = jsonObject["mimeType"]?.AsString();
        Icons = jsonObject["icons"].AsArray(o => new Icon(o));

        var metaObj = jsonObject["_meta"]?.AsObject();
        if (metaObj != null)
            Meta = new Meta(metaObj);
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("uri", Uri);
        jsonWriter.Write("name", Name);
        Title?.WriteTo(jsonWriter, "title");
        Description?.WriteTo(jsonWriter, "description");
        MimeType?.WriteTo(jsonWriter, "mimeType");
        if (Icons is { Length: > 0 })
            Icons.WriteTo(jsonWriter, "icons");
        Meta?.WriteTo(jsonWriter, "_meta");
    }
}
