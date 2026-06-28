namespace McpSdk.Protocol.Models.ClientCapabilities
{
    public sealed class ClientCapabilitiesModel : IJsonObjectWriter
    {
        public RootsCapabilityModel RootsCapability { get; set; }
        public SamplingCapabilityModel SamplingCapability { get; set; }
        public ElicitationCapabilityModel ElicitationCapability { get; set; }

        public ClientCapabilitiesModel() { }

        public ClientCapabilitiesModel(IJsonObject jsonObject)
        {
            var rootsCapability = jsonObject["roots"]?.AsObject();
            if (rootsCapability != null)
                RootsCapability = new RootsCapabilityModel(rootsCapability);

            var samplingCapability = jsonObject["sampling"]?.AsObject();
            if (samplingCapability != null)
                SamplingCapability = new SamplingCapabilityModel(samplingCapability);

            var elicitationCapability = jsonObject["elicitation"]?.AsObject();
            if (elicitationCapability != null)
                ElicitationCapability = new ElicitationCapabilityModel(elicitationCapability);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            RootsCapability?.WriteTo(writer, "roots");
            SamplingCapability?.WriteTo(writer, "sampling");
            ElicitationCapability?.WriteTo(writer, "elicitation");
        }
    }
}