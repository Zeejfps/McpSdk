namespace McpSdk.Protocol.Models;

public sealed class TextResourceContents : ResourceContents
{
    public string Text { get; }

    public TextResourceContents(string uri, string mimeType, string text) : base(uri, mimeType)
    {
        Text = text;
    }
        
    public TextResourceContents(IJsonObject jsonObject) : base(jsonObject)
    {
        Text = jsonObject["text"]?.AsString();
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        base.WriteMembers(writer);

        Text?.WriteTo(writer, "text");
    }
}