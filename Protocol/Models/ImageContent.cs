using System;

namespace McpSdk.Protocol.Models;

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

    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "image");
        writer.Write("mimeType", MimeType);
        writer.Write("data", Base64EncodedData);
    }

    public string Base64EncodedData { get; }
    public string MimeType { get; }
}