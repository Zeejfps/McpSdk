namespace McpSharp.Protocol
{
    public enum ContentKind
    {
        Text,
        Image,
        Resource
    }

    public abstract class Content
    {
        public abstract ContentKind Kind { get; }
    }

    public sealed class TextContent : Content
    {
        public override ContentKind Kind => ContentKind.Text;
        public string Text { get; }
    }

    public sealed class ImageContent : Content
    {
        public override ContentKind Kind => ContentKind.Image;
        public string Data { get; }
        public string MimeType { get; }
    }

    public sealed class ResourceContent : Content
    {
        public override ContentKind Kind => ContentKind.Resource;
        public Resource Resource { get; }
    }

    public sealed class Resource
    {
        public string Uri { get; }
        public string MimeType { get; }
        public string Text { get; }
    }
}