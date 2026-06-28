namespace McpSdk.Protocol.Models;

/// <summary>
/// The params of a <c>notifications/progress</c> notification: the <c>progressToken</c> the caller
/// attached to the original request (string or number), the current <c>progress</c> amount, an
/// optional <c>total</c>, and an optional human-readable <c>message</c>.
/// </summary>
public sealed class ProgressNotification : IJsonObjectWriter
{
    public RequestId ProgressToken { get; }
    public double Progress { get; }
    public double? Total { get; }
    public string Message { get; }

    public ProgressNotification(RequestId progressToken, double progress, double? total = null, string message = null)
    {
        ProgressToken = progressToken;
        Progress = progress;
        Total = total;
        Message = message;
    }

    public ProgressNotification(IJsonObject jsonObject)
    {
        ProgressToken = RequestId.FromJson(jsonObject["progressToken"]);
        Progress = jsonObject["progress"].AsDouble();
        Total = jsonObject["total"]?.AsDouble();
        Message = jsonObject["message"]?.AsString();
    }

    public void WriteMembers(IJsonWriter writer)
    {
        ProgressToken.WriteTo(writer, "progressToken");
        writer.Write("progress", Progress);
        Total?.WriteTo(writer, "total");
        if (Message != null)
            Message.WriteTo(writer, "message");
    }
}
