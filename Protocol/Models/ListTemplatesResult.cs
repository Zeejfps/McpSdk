using System;
using System.Linq;

namespace McpSdk.Protocol.Models;

public sealed class ListTemplatesResult : IJsonObjectWriter
{
    public ResourceTemplate[] ResourceTemplates { get; }

    /// <summary>Opaque cursor for the next page (2025-11-25), or null when this is the last page.</summary>
    public string NextCursor { get; }

    public ListTemplatesResult(ResourceTemplate[] resourceTemplates, string nextCursor = null)
    {
        ResourceTemplates = resourceTemplates ?? Array.Empty<ResourceTemplate>();
        NextCursor = nextCursor;
    }

    public ListTemplatesResult(IJsonObject jsonObject)
    {
        ResourceTemplates = jsonObject["resourceTemplates"]?.AsObjectArray()
            ?.Select(t => new ResourceTemplate(t)).ToArray() ?? Array.Empty<ResourceTemplate>();
        NextCursor = jsonObject?["nextCursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        ResourceTemplates.WriteTo(writer, "resourceTemplates");
        NextCursor?.WriteTo(writer, "nextCursor");
    }
}
