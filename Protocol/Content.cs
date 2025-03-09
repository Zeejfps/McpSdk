namespace McpSharp.Protocol
{
    public abstract class Content
    {
        public abstract string Type { get; }
    }

    public sealed class TextContent : Content
    {
        public override string Type => "text";
        public string Text { get; }
    }

    public sealed class ImageContent : Content
    {
        public override string Type => "image";
        public string Data { get; }
        public string MimeType { get; }
    }

    public sealed class ResourceContent : Content
    {
        public override string Type => "resource";
        public Resource Resource { get; }
    }

    public sealed class Resource
    {
        public string Uri { get; }
        public string MimeType { get; }
        public string Text { get; }
    }
}