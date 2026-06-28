using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class ResourceReference : ReferenceModel
{
    public string Uri { get; }

    public ResourceReference(string uri)
    {
        Uri = uri;
    }

    public ResourceReference(IJsonObject jsonObject)
    {
        Uri = jsonObject["uri"].AsString();
    }

    public override void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("type", "ref/resource");
        jsonWriter.Write("uri", Uri);
    }
}