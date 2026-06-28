namespace McpSdk.Protocol.Models.ClientCapabilities
{
    /// <summary>
    /// The <c>elicitation</c> client capability (2025-11-25). A client advertises which interaction
    /// modes it can satisfy: <c>form</c> (in-band structured data collection) and/or <c>url</c>
    /// (out-of-band navigation). Per spec, an empty capability object is equivalent to declaring
    /// <c>form</c>-mode support only, and a declaring client MUST support at least one mode.
    /// </summary>
    public sealed class ElicitationCapabilityModel : IJsonObjectWriter
    {
        public bool SupportsForm { get; set; } = true;
        public bool SupportsUrl { get; set; }

        public ElicitationCapabilityModel() { }

        public ElicitationCapabilityModel(bool supportsForm, bool supportsUrl)
        {
            SupportsForm = supportsForm;
            SupportsUrl = supportsUrl;
        }

        public ElicitationCapabilityModel(IJsonObject jsonObject)
        {
            var form = jsonObject["form"]?.AsObject();
            var url = jsonObject["url"]?.AsObject();

            // An empty object ({}) means form-mode only; otherwise honour the modes present.
            if (form == null && url == null)
            {
                SupportsForm = true;
                SupportsUrl = false;
            }
            else
            {
                SupportsForm = form != null;
                SupportsUrl = url != null;
            }
        }

        public void WriteMembers(IJsonWriter writer)
        {
            if (SupportsForm)
                writer.Write("form", _ => { });
            if (SupportsUrl)
                writer.Write("url", _ => { });
        }
    }
}
