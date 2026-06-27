namespace McpSdk.Protocol.Models;

/// <summary>
/// A resource-link content block (2025-06-18): a pointer to a resource the client can read later
/// via <c>resources/read</c>, rather than embedding its contents inline. Carries the same
/// descriptive fields as a <see cref="Resource"/> but never the body.
/// </summary>
public sealed class ResourceLinkContent : Content
{
    public string Uri { get; }
    public string Name { get; }
    public string Title { get; }
    public string Description { get; }
    public string MimeType { get; }

    public ResourceLinkContent(string uri, string name, string title = null, string description = null, string mimeType = null)
    {
        Uri = uri;
        Name = name;
        Title = title;
        Description = description;
        MimeType = mimeType;
    }

    public ResourceLinkContent(IJsonObject jsonObject)
    {
        Uri = jsonObject["uri"]?.AsString();
        Name = jsonObject["name"]?.AsString();
        Title = jsonObject["title"]?.AsString();
        Description = jsonObject["description"]?.AsString();
        MimeType = jsonObject["mimeType"]?.AsString();
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", "resource_link");
        writer.Write("uri", Uri);
        writer.Write("name", Name);
        if (Title != null)
            writer.Write("title", Title);
        if (Description != null)
            writer.Write("description", Description);
        if (MimeType != null)
            writer.Write("mimeType", MimeType);
    }
}
