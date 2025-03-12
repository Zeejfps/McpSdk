namespace McpSdk.Protocol.Models
{
    public sealed class SamplingCapability : JsonObjectWrapper
    {
        public SamplingCapability(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
        }

        public override IJsonObject JsonObject { get; }
    }
}