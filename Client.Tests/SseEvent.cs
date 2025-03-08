using System.Text;
using McpSharp.Client;

namespace Client.Tests;

sealed class SseEvent : ISseEvent
{
    public string? Id { get; set; }
    public string Kind { get; }
    public string? Data { get; set; }

    public SseEvent(string kind)
    {
        Kind = kind;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("event: ").Append(Kind).Append(' ');
        if (Id != null)
            sb.Append("id: ").Append(Id).Append(' ');
        if (Data != null)
            sb.Append("data: ").Append(Data).Append(' ');
        return sb.ToString();
    }
}