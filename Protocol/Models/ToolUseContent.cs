namespace McpSdk.Protocol.Models;

/// <summary>
/// A model's request to call a tool, returned in a sampling result (and echoed back in the assistant
/// message of a follow-up request) when sampling-with-tools is in use (2025-11-25, SEP-1577).
/// </summary>
public sealed class ToolUseContent : Content
{
    /// <summary>An identifier the matching <see cref="ToolResultContent.ToolUseId"/> echoes.</summary>
    public string Id { get; }
    public string Name { get; }

    /// <summary>The tool arguments, carried as an opaque JSON object.</summary>
    public IJsonObject Input { get; }

    public ToolUseContent(IJsonObject jsonObject)
    {
        Id = jsonObject["id"]?.AsString();
        Name = jsonObject["name"]?.AsString();
        Input = jsonObject["input"]?.AsObject();
    }

    public ToolUseContent(string id, string name, IJsonObject input)
    {
        Id = id;
        Name = name;
        Input = input;
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        writer.Write("type", "tool_use");
        writer.Write("id", Id);
        writer.Write("name", Name);
        if (Input != null)
            writer.Write("input", Input);
    }
}
