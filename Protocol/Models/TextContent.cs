namespace McpSdk.Protocol.Models;

public sealed class TextContent : Content
{
    public TextContent(IJsonObject jsonObject)
    {
        Text = jsonObject["text"]?.AsString();
    }

    public TextContent(string text)
    {
        Text = text;
    }

    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "text");
        writer.Write("text", Text);
    }

    public string Text { get; }
}