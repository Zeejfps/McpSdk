using System.Text;

namespace McpSdk.Server;

public sealed class SseEvent
{
    public string Id { get; set; }
    public string Kind { get; set; }
    public string Data { get; set; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("event: ").Append(Kind).AppendLine();
        if (Id != null)
            sb.Append("id: ").Append(Id).AppendLine();
        if (Data != null)
            sb.Append("data: ").Append(Data).AppendLine();
        return sb.ToString();
    }
}