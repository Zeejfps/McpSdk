namespace McpSdk.Protocol.Models;

public sealed class BlobResourceContents : ResourceContents
{
    public string Blob { get; }

    public BlobResourceContents(string uri, string mimeType, string blob) : base(uri, mimeType)
    {
        Blob = blob;
    }
        
    public BlobResourceContents(IJsonObject jsonObject) : base(jsonObject)
    {
        Blob = jsonObject["blob"]?.AsString();
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        base.WriteMembers(writer);

        Blob?.WriteTo(writer, "blob");
    }
}