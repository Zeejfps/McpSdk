namespace McpSdk.Protocol.Models;

public sealed class Resource : IJsonObjectWriter
{
    public string Uri { get; }
    public string Name { get; }
    public string Description { get; }
    public string MimeType { get; }

    public Resource(string uri, string name, string description, string mimeType)
    {
        Uri = uri;
        Name = name;
        Description = description;
        MimeType = mimeType;
    }

    public Resource(IJsonObject jsonObject)
    {
        Uri = jsonObject["uri"].AsString();
        Name = jsonObject["name"].AsString();
        Description = jsonObject["description"].AsString();
        MimeType = jsonObject["mimeType"].AsString();
    }
    
    public void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("uri", Uri);
        jsonWriter.Write("name", Name);
        jsonWriter.Write("description", Description);
        jsonWriter.Write("mimeType", MimeType);
    }
}