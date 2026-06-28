namespace McpSdk.Protocol.Models;

/// <summary>
/// Params for <c>prompts/get</c>: the prompt <c>name</c> to render plus an optional opaque
/// <c>arguments</c> map of substitution values.
/// </summary>
public sealed class GetPromptRequest : IJsonObjectWriter
{
    public string Name { get; }

    /// <summary>Opaque map of argument name → value; null when the prompt takes none.</summary>
    public IJsonObject Arguments { get; }

    public GetPromptRequest(string name, IJsonObject arguments = null)
    {
        Name = name;
        Arguments = arguments;
    }

    public GetPromptRequest(IJsonObject jsonObject)
    {
        Name = jsonObject["name"].AsString();
        Arguments = jsonObject["arguments"]?.AsObject();
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("name", Name);
        Arguments?.WriteTo(jsonWriter, "arguments");
    }
}
