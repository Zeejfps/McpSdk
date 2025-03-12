using System;

namespace McpSdk.Protocol.Models
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
        public abstract ContentKind Kind { get; }

        public abstract void ToJson(IJsonWriter writer);
        
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
            Text = jsonObject["text"]?.AsString();
        }

        public TextContent(string text)
        {
            Text = text;
        }

        public override void ToJson(IJsonWriter writer)
        {
            writer.Write("type", "text");
            writer.Write("text", Text);
        }

        public override ContentKind Kind => ContentKind.Text;
        public string Text { get; }
    }

    public sealed class ImageContent : Content
    {
        public ImageContent(IJsonObject jsonObject)
        {
            MimeType = jsonObject["mimeType"].AsString();
            Base64EncodedData = jsonObject["data"].AsString();
        }

        public ImageContent(string mimeType, byte[] data)
        {
            MimeType = mimeType;
            Base64EncodedData = Convert.ToBase64String(data);
        }

        public override ContentKind Kind => ContentKind.Image;
        public override void ToJson(IJsonWriter writer)
        {
            writer.Write("type", "image");
            writer.Write("mimeType", MimeType);
            writer.Write("data", Base64EncodedData);
        }

        public string Base64EncodedData { get; }
        public string MimeType { get; }
    }

    public sealed class ResourceContent : Content
    {
        public override ContentKind Kind => ContentKind.Resource;
        public Resource Resource { get; }
        
        public ResourceContent(IJsonObject jsonObject)
        {
            var resourceObj = jsonObject["resource"].AsObject();
            Resource = new Resource(resourceObj);
        }

        public override void ToJson(IJsonWriter writer)
        {
            writer.Write("type", "resource");
            writer.Write("resource", Resource.ToJson);
        }
    }

    public sealed class UnknownContent : Content
    {
        public UnknownContent(IJsonObject jsonObject)
        {
        }

        public override ContentKind Kind => ContentKind.Unknown;
        public override void ToJson(IJsonWriter writer)
        {
            writer.Write("type", "unknown");
        }
    }
}