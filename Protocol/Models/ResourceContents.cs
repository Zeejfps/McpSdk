using System;

namespace McpSdk.Protocol.Models
{
    public abstract class ResourceContents : IJsonSerializable
    {
        public string Uri { get; }
        public string MimeType { get; }

        public ResourceContents(string uri, string mimeType)
        {
            Uri = uri;
            MimeType = mimeType;
        }
        
        public ResourceContents(IJsonObject jsonObject)
        {
            Uri = jsonObject["uri"].AsString();
            MimeType = jsonObject["mimeType"].AsString();
        }

        public virtual void AsJson(IJsonWriter writer)
        {
            writer.Write("uri", Uri);
            writer.Write("mimeType", MimeType);
        }

        public static ResourceContents FromJsonObject(IJsonObject resourceObj)
        {
            if (resourceObj["text"] != null)
                return new TextResourceContents(resourceObj);
            if (resourceObj["blob"] != null)
                return new BlobResourceContents(resourceObj);

            throw new Exception("Unknown embedded resource contents type");
        }
    }
}