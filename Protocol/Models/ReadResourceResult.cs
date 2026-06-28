using System;

namespace McpSdk.Protocol.Models;

/// <summary>
/// Result of <c>resources/read</c>: the resource's <c>contents</c> (one entry per returned resource —
/// a single URI may yield several, e.g. a directory) plus opaque <c>_meta</c>. Each entry is a
/// <see cref="TextResourceContents"/> or <see cref="BlobResourceContents"/>, parsed back via
/// <see cref="ResourceContents.FromJsonObject"/>.
/// </summary>
public sealed class ReadResourceResult : IJsonObjectWriter
{
    public ResourceContents[] Contents { get; }

    /// <summary>Opaque, implementation-defined metadata.</summary>
    public Meta Meta { get; }

    public ReadResourceResult(ResourceContents[] contents, Meta meta = null)
    {
        Contents = contents ?? Array.Empty<ResourceContents>();
        Meta = meta;
    }

    public ReadResourceResult(IJsonObject jsonObject)
    {
        Contents = jsonObject["contents"].AsArray(ResourceContents.FromJsonObject) ?? Array.Empty<ResourceContents>();

        var metaObj = jsonObject["_meta"]?.AsObject();
        if (metaObj != null)
            Meta = new Meta(metaObj);
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        Contents.WriteTo(jsonWriter, "contents");
        Meta?.WriteTo(jsonWriter, "_meta");
    }
}
