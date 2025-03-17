using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class PromptReference : ReferenceModel
{
    public string Name { get; }

    public PromptReference(string name)
    {
        Name = name;
    }

    public PromptReference(IJsonObject jsonObject)
    {
        Name = jsonObject["name"].AsString();
    }

    public override void AsJson(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("type", "ref/prompt");
        jsonWriter.Write("name", Name);
    }
}