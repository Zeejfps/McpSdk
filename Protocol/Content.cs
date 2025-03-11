namespace McpSharp.Protocol
{
    public enum ContentKind
    {
        Unknown,
        Text,
        Image,
        Resource
    }

    public abstract class Content
    {
        protected Content(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
        }

        public abstract ContentKind Kind { get; }
        
        public IJsonObject JsonObject { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }

    public sealed class TextContent : Content
    {
        public TextContent(IJsonObject jsonObject) : base(jsonObject)
        {
            Text = jsonObject["text"].AsString();
        }

        public override ContentKind Kind => ContentKind.Text;
        public string Text { get; }
    }

    public sealed class ImageContent : Content
    {
        public ImageContent(IJsonObject jsonObject) : base(jsonObject)
        {
            MimeType = jsonObject["mimeType"].AsString();
            Data = jsonObject["data"].AsString();
        }

        public override ContentKind Kind => ContentKind.Image;
        public string Data { get; }
        public string MimeType { get; }
    }

    public sealed class ResourceContent : Content
    {
        public ResourceContent(IJsonObject jsonObject) : base(jsonObject)
        {
            var resourceObj = jsonObject["resource"].AsObject();
            Resource = new Resource(resourceObj);
        }

        public override ContentKind Kind => ContentKind.Resource;
        public Resource Resource { get; }
    }

    public sealed class UnknownContent : Content
    {
        public UnknownContent(IJsonObject jsonObject) : base(jsonObject)
        {
        }

        public override ContentKind Kind => ContentKind.Unknown;
    }

    public sealed class Resource
    {
        public Resource(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            Uri = jsonObject["uri"].AsString();
            MimeType = jsonObject["mimeType"].AsString();
            Text = jsonObject["text"].AsString();
        }
        
        public IJsonObject JsonObject { get; }
        public string Uri { get; }
        public string MimeType { get; }
        public string Text { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }
}