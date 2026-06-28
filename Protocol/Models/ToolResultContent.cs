namespace McpSdk.Protocol.Models;

/// <summary>
/// The result of a tool call, supplied in a follow-up sampling request's user message when
/// sampling-with-tools is in use (2025-11-25, SEP-1577). Its <see cref="ToolUseId"/> matches the
/// <see cref="ToolUseContent.Id"/> it answers. Per spec, a user message carrying tool results must
/// contain <em>only</em> tool results.
/// </summary>
public sealed class ToolResultContent : Content
{
    public string ToolUseId { get; }

    /// <summary>The tool's output, as a content block array (text/image/audio/etc.).</summary>
    public Content[] Content { get; }

    public ToolResultContent(IJsonObject jsonObject)
    {
        ToolUseId = jsonObject["toolUseId"]?.AsString();
        Content = jsonObject["content"].AsArray(Models.Content.FromJsonObject) ?? System.Array.Empty<Content>();
    }

    public ToolResultContent(string toolUseId, params Content[] content)
    {
        ToolUseId = toolUseId;
        Content = content;
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", "tool_result");
        writer.Write("toolUseId", ToolUseId);
        Content.WriteTo(writer, "content");
    }
}
