using System;
using System.Linq;

namespace McpSdk.Protocol.Models;

/// <summary>
/// Result of <c>prompts/get</c>: the rendered conversation (<c>messages</c>) plus an optional
/// <c>description</c> and opaque <c>_meta</c>.
/// </summary>
public sealed class GetPromptResult : IJsonObjectWriter
{
    public string Description { get; }
    public PromptMessage[] Messages { get; }

    /// <summary>Opaque, implementation-defined metadata.</summary>
    public Meta Meta { get; }

    public GetPromptResult(PromptMessage[] messages, string description = null, Meta meta = null)
    {
        Messages = messages ?? Array.Empty<PromptMessage>();
        Description = description;
        Meta = meta;
    }

    public GetPromptResult(IJsonObject jsonObject)
    {
        Description = jsonObject["description"]?.AsString();
        Messages = jsonObject["messages"]?.AsObjectArray()
            ?.Select(m => new PromptMessage(m)).ToArray() ?? Array.Empty<PromptMessage>();

        var metaObj = jsonObject["_meta"]?.AsObject();
        if (metaObj != null)
            Meta = new Meta(metaObj);
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        Description?.WriteTo(jsonWriter, "description");
        Messages.WriteTo(jsonWriter, "messages");
        Meta?.WriteTo(jsonWriter, "_meta");
    }
}
