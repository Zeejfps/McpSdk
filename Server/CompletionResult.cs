using McpSdk.Protocol;

namespace McpSdk.Server;

/// <summary>
/// The body of a <c>completion/complete</c> result — the <c>completion</c> object the server returns:
/// up to 100 suggested <c>values</c>, an optional <c>total</c> count (may exceed the values sent), and
/// <c>hasMore</c> (more options exist beyond those returned, even if the exact total is unknown).
/// <see cref="McpServer"/> nests this under a <c>completion</c> property, per spec.
/// </summary>
public sealed class CompletionResult : IJsonObjectWriter
{
    public string[] Values { get; }
    public int? Total { get; }
    public bool HasMore { get; }

    public CompletionResult(string[] values, int? total = null, bool hasMore = false)
    {
        Values = values;
        Total = total;
        HasMore = hasMore;
    }

    public CompletionResult(IJsonObject jsonObject)
    {
        Values = jsonObject["values"].AsStringArray();
        Total = jsonObject["total"]?.AsInt();
        HasMore = jsonObject["hasMore"]?.AsBool() ?? false;
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("values", Values);
        Total?.WriteTo(jsonWriter, "total");
        jsonWriter.Write("hasMore", HasMore);
    }
}
