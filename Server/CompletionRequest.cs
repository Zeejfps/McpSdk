using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class CompletionRequest
{
    public ReferenceModel Reference { get; }
    public IJsonObject Arguments { get; }

    public CompletionRequest(ReferenceModel reference, IJsonObject arguments)
    {
        Reference = reference;
        Arguments = arguments;
    }

    public CompletionRequest(IJsonObject jsonObject)
    {
        Reference = ReferenceModel.FromJsonObject(jsonObject["ref"].AsObject());
        Arguments = jsonObject["arguments"].AsObject();
    }

    public void AsJson(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("ref", Reference.AsJson);
        jsonWriter.Write("arguments", Arguments);
    }
}