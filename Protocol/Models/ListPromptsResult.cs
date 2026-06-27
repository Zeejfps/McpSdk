using System;
using System.Linq;

namespace McpSdk.Protocol.Models;

public sealed class ListPromptsResult : IJsonObjectWriter
{
    public Prompt[] Prompts { get; }

    /// <summary>Opaque cursor for the next page (2025-11-25), or null when this is the last page.</summary>
    public string NextCursor { get; }

    public ListPromptsResult(Prompt[] prompts, string nextCursor = null)
    {
        Prompts = prompts ?? Array.Empty<Prompt>();
        NextCursor = nextCursor;
    }

    public ListPromptsResult(IJsonObject jsonObject)
    {
        Prompts = jsonObject["prompts"]?.AsObjectArray()
            ?.Select(p => new Prompt(p)).ToArray() ?? Array.Empty<Prompt>();
        NextCursor = jsonObject?["nextCursor"]?.AsString();
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        Prompts.WriteTo(jsonWriter, "prompts");
        NextCursor?.WriteTo(jsonWriter, "nextCursor");
    }
}
