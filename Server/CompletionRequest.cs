using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class CompletionRequest : IJsonObjectWriter
{
    public ReferenceModel Reference { get; }

    /// <summary>
    /// The single argument being completed, an object <c>{ name, value }</c>. The wire field is the
    /// spec's singular <c>argument</c> (not <c>arguments</c>) — it describes one argument, not a map.
    /// </summary>
    public IJsonObject Arguments { get; }

    /// <summary>
    /// Previously resolved argument values (2025-06-18) that narrow the server's suggestions, or null
    /// when the caller supplied none.
    /// </summary>
    public CompletionContext Context { get; }

    public CompletionRequest(ReferenceModel reference, IJsonObject arguments, CompletionContext context = null)
    {
        Reference = reference;
        Arguments = arguments;
        Context = context;
    }

    public CompletionRequest(IJsonObject jsonObject)
    {
        Reference = ReferenceModel.FromJsonObject(jsonObject["ref"].AsObject());
        Arguments = jsonObject["argument"].AsObject();

        var contextObj = jsonObject["context"]?.AsObject();
        if (contextObj != null)
            Context = CompletionContext.FromJson(contextObj);
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("ref", Reference);
        jsonWriter.Write("argument", Arguments);
        Context?.WriteTo(jsonWriter, "context");
    }
}