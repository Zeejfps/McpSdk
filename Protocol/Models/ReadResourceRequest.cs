namespace McpSdk.Protocol.Models;

/// <summary>
/// Params of <c>resources/read</c>: the <c>uri</c> of the resource to read.
/// </summary>
public sealed class ReadResourceRequest : IJsonObjectWriter
{
    public string Uri { get; }

    public ReadResourceRequest(string uri)
    {
        Uri = uri;
    }

    public ReadResourceRequest(IJsonObject jsonObject)
    {
        Uri = jsonObject["uri"]?.AsString();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        Uri?.WriteTo(writer, "uri");
    }
}
