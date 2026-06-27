using McpSdk.Protocol;

namespace McpSdk.Server;

/// <summary>
/// The optional <c>context</c> on a <c>completion/complete</c> request (2025-06-18): previously
/// resolved argument values that let the server narrow its suggestions (e.g. a chosen repo owner
/// constraining the completions for a repo name). <c>Arguments</c> is an opaque map of argument
/// name → already-supplied value.
/// </summary>
public sealed class CompletionContext : IJsonObjectWriter
{
    public IJsonObject Arguments { get; }

    private CompletionContext(IJsonObject arguments)
    {
        Arguments = arguments;
    }

    /// <summary>Builds a context from an already-resolved argument map.</summary>
    public static CompletionContext FromArguments(IJsonObject arguments) => new(arguments);

    /// <summary>Parses the <c>context</c> object, reading its <c>arguments</c> map.</summary>
    public static CompletionContext FromJson(IJsonObject contextJson) => new(contextJson?["arguments"]?.AsObject());

    public void WriteMembers(IJsonWriter writer)
    {
        if (Arguments != null)
            writer.Write("arguments", Arguments);
    }
}
