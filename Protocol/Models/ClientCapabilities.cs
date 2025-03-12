namespace McpSdk.Protocol.Models
{
    public sealed class ClientCapabilities
    {
        public ClientCapabilities()
        {
            
        }
        
        public ClientCapabilities(IJsonObject jsonObject)
        {
            var rootsCapability = jsonObject["roots"]?.AsObject();
            if (rootsCapability != null)
                RootsCapability = new RootsCapability(rootsCapability);
            
            var samplingCapability = jsonObject["sampling"]?.AsObject();
            if (samplingCapability != null)
                SamplingCapability = new SamplingCapability(samplingCapability);
        }
        
        public RootsCapability RootsCapability { get; set; }
        public SamplingCapability SamplingCapability { get; set; }
        
        public void ToJson(IJsonWriter writer)
        {
            if (RootsCapability != null)
                writer.Write("roots", RootsCapability.ToJson);

            if (SamplingCapability != null)
                writer.Write("sampling", SamplingCapability.ToJson);
        }
    }
}