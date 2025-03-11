using System;

namespace McpSdk.Protocol
{
    public enum ContentKind
    {
        Unknown,
        Text,
        Image,
        Resource
    }

    public abstract class Content : JsonObjectWrapper
    {
        public abstract ContentKind Kind { get; }

        public static Content Create(IJsonObject jsonObject)
        {
            var type = jsonObject["type"].AsString();
            if (type == "text")
            {
                return new TextContent(jsonObject);
            }
            
            if (type == "image")
            {
                return new ImageContent(jsonObject);
            }
             
            if (type == "resource")
            {
                return new ResourceContent(jsonObject);
            }
            
            return new UnknownContent(jsonObject);
        }
    }

    public sealed class TextContent : Content
    {
        public TextContent(IJsonObject jsonObject)
        {
            Text = jsonObject["text"].AsString();
            JsonObject = jsonObject;
        }

        public TextContent(IJson json, string text)
        {
            Text = text;
            JsonObject = json.Build(props =>
            {
                props.Write("type", "text");
                props.Write("text", text);
            });
        }

        public override IJsonObject JsonObject { get; }
        public override ContentKind Kind => ContentKind.Text;
        public string Text { get; }
    }

    public sealed class ImageContent : Content
    {
        public ImageContent(IJsonObject jsonObject)
        {
            MimeType = jsonObject["mimeType"].AsString();
            Data = jsonObject["data"].AsString();
            JsonObject = jsonObject;
        }

        public ImageContent(IJson json, string mimeType, byte[] data)
        {
            MimeType = mimeType;
            Data = Convert.ToBase64String(data);
            JsonObject = json.Build(props =>
            {
                props.Write("type", "image");
                props.Write("mimeType", mimeType);
                props.Write("data", Data);
            });
        }

        public override ContentKind Kind => ContentKind.Image;
        public string Data { get; }
        public string MimeType { get; }
        public override IJsonObject JsonObject { get; }
    }

    public sealed class ResourceContent : Content
    {
        public ResourceContent(IJsonObject jsonObject)
        {
            var resourceObj = jsonObject["resource"].AsObject();
            Resource = new Resource(resourceObj);
            JsonObject = jsonObject;
        }

        public override ContentKind Kind => ContentKind.Resource;
        public Resource Resource { get; }
        public override IJsonObject JsonObject { get; }
    }

    public sealed class UnknownContent : Content
    {
        public UnknownContent(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
        }

        public override ContentKind Kind => ContentKind.Unknown;
        public override IJsonObject JsonObject { get; }
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