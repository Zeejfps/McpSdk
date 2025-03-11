namespace McpSharp.Protocol
{
    public sealed class CreateMessagesResult : JsonObjectWrapper
    {
        public CreateMessagesResult(IJsonObject jsonObject) : base(jsonObject)
        {
            Role = jsonObject["Role"]?.AsString();
            Model = jsonObject["Module"]?.AsString();
            StopReason = jsonObject["StopReason"]?.AsString();
        }

        public string Role { get; }
        public string Model { get; }
        public string StopReason { get; }
        public Content Content { get; }
    }
}