namespace McpSdk.Protocol.Models.ClientCapabilities
{
    public sealed class ClientCapabilitiesModel : IJsonSerializable
    {
        public RootsCapabilityModel RootsCapability { get; set; }
        public SamplingCapabilityModel SamplingCapability { get; set; }
        
        public ClientCapabilitiesModel() { }
        
        public ClientCapabilitiesModel(IJsonObject jsonObject)
        {
            var rootsCapability = jsonObject["roots"]?.AsObject();
            if (rootsCapability != null)
                RootsCapability = new RootsCapabilityModel(rootsCapability);
            
            var samplingCapability = jsonObject["sampling"]?.AsObject();
            if (samplingCapability != null)
                SamplingCapability = new SamplingCapabilityModel(samplingCapability);
        }
        
        public void AsJson(IJsonWriter writer)
        {
            if (RootsCapability != null)
                writer.Write("roots", RootsCapability);

            if (SamplingCapability != null)
                writer.Write("sampling", SamplingCapability);
        }
    }
}