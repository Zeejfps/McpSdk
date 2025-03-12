namespace McpSdk.Protocol.Models
{
    public sealed class ClientCapabilities : JsonObjectWrapper
    {
        public ClientCapabilities(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            
            var rootsCapability = jsonObject["roots"]?.AsObject();
            if (rootsCapability != null)
                RootsCapability = new RootsCapability(rootsCapability);
            
            var samplingCapability = jsonObject["sampling"]?.AsObject();
            if (samplingCapability != null)
                SamplingCapability = new SamplingCapability(samplingCapability);
        }
        
        public RootsCapability RootsCapability { get; }
        public SamplingCapability SamplingCapability { get; }

        public static Writer CreateWriter(IJsonWriter writer)
        {
            return new Writer(writer);
        }
        
        public sealed class Writer
        {
            private readonly IJsonWriter _writer;

            internal Writer(IJsonWriter writer)
            {
                _writer = writer;
            }
        
            public Writer WriteRootsCapability(bool isListChangedNotificationSupported)
            {
                _writer.Write("roots", roots =>
                {
                    roots.Write("listChanged", isListChangedNotificationSupported);
                });
                return this;
            }

            public Writer WriteSamplingCapability()
            {
                _writer.Write("sampling", sampling => { });
                return this;
            }
        }

        public override IJsonObject JsonObject { get; }
    }
}