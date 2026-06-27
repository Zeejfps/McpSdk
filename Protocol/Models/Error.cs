namespace McpSdk.Protocol.Models;

public sealed class Error : IJsonObjectWriter
{
    public ErrorCode Code { get;}
    public string Message { get;}
    public IJsonObject Data { get; }

    public Error(IJsonObject jsonObject)
    {
        Code = (ErrorCode)jsonObject["code"].AsInt();
        Message = jsonObject["message"].AsString();
        Data = jsonObject["data"]?.AsObject();
    }
    
    public Error(ErrorCode code, string message, IJsonObject data = null)
    {
        Code = code;
        Message = message;
        Data = data;
    }

    public void WriteMembers(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("code", (int)Code);
        jsonWriter.Write("message", Message);
        Data?.WriteTo(jsonWriter, "data");
    }

    /// <summary>Writes this error as the named property on the supplied writer.</summary>
    public void WriteTo(IJsonWriter writer, string propertyName) => writer.Write(propertyName, WriteMembers);
}