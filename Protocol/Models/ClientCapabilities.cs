namespace McpSdk.Protocol.Models
{
    public sealed class ClientCapabilities
    {
        public ClientCapabilities(IJsonObject jsonObject)
        {
            
        }

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
    }
}