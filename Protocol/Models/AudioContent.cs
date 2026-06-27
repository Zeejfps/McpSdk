using System;

namespace McpSdk.Protocol.Models;

/// <summary>Audio content (2025-03-26): base64-encoded audio data with a MIME type.</summary>
public sealed class AudioContent : Content
{
    public AudioContent(IJsonObject jsonObject)
    {
        MimeType = jsonObject["mimeType"].AsString();
        Base64EncodedData = jsonObject["data"].AsString();
    }

    public AudioContent(string mimeType, byte[] data)
    {
        MimeType = mimeType;
        Base64EncodedData = Convert.ToBase64String(data);
    }

    public AudioContent(string mimeType, string base64EncodedData)
    {
        MimeType = mimeType;
        Base64EncodedData = base64EncodedData;
    }

    public override void AsJson(IJsonWriter writer)
    {
        writer.Write("type", "audio");
        writer.Write("mimeType", MimeType);
        writer.Write("data", Base64EncodedData);
    }

    public string Base64EncodedData { get; }
    public string MimeType { get; }
}
