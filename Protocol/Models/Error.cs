namespace McpSdk.Protocol.Models;

public sealed class Error
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

    public void AsJson(IJsonWriter jsonWriter)
    {
        jsonWriter.Write("code", (int)Code);
        jsonWriter.Write("message", Message);
        if (Data != null)
            jsonWriter.Write("data", Data);
    }
}