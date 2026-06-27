using McpSdk.Protocol;

namespace McpSdk.Server;

public sealed class CompletionResult : IJsonObjectWriter
{
    public string[] Values { get; }
    public int? TotalMatches { get; }
    public bool HasMoreMatches { get; }

    public CompletionResult(string[] values, int? totalMatches, bool hasMoreMatches)
    {
        Values = values;
        TotalMatches = totalMatches;
        HasMoreMatches = hasMoreMatches;
    }

    public CompletionResult(IJsonObject jsonObject)
    {
        Values = jsonObject["values"].AsStringArray();
        TotalMatches = jsonObject["totalMatches"]?.AsInt();
        HasMoreMatches = jsonObject["hasMoreMatches"].AsBool();
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("values", Values);
        TotalMatches?.WriteTo(jsonWriter, "totalMatches");
        jsonWriter.Write("hasMoreMatches", HasMoreMatches);
    }
}