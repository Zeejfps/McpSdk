namespace McpSdk.Protocol.Models;

/// <summary>
/// The catch-all <see cref="Content"/> for a <c>type</c> this SDK does not model. It retains the
/// source JSON object and re-emits it verbatim, so an unrecognized content block round-trips intact
/// rather than being flattened to <c>{"type":"unknown"}</c> — a forward-compat peer keeps the data
/// it received.
/// </summary>
public sealed class UnknownContent : Content
{
    private readonly IJsonObject _source;

    public UnknownContent(IJsonObject jsonObject)
    {
        _source = jsonObject;
    }

    public override void WriteMembers(IJsonWriter writer)
    {
        _source.WriteMembers(writer);
    }
}
