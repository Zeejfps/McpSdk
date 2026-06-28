namespace McpSdk.Protocol.Models.ClientCapabilities
{
    public sealed class SamplingCapabilityModel : IJsonObjectWriter
    {
        /// <summary>When true, the client can service tool-enabled sampling requests (2025-11-25, SEP-1577).</summary>
        public bool SupportsTools { get; set; }

        public SamplingCapabilityModel()
        {

        }

        public SamplingCapabilityModel(bool supportsTools)
        {
            SupportsTools = supportsTools;
        }

        public SamplingCapabilityModel(IJsonObject jsonObject)
        {
            SupportsTools = jsonObject["tools"]?.AsObject() != null;
        }

        public void WriteMembers(IJsonWriter writer)
        {
            if (SupportsTools)
                writer.Write("tools", _ => { });
        }
    }
}
