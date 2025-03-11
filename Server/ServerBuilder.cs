using System;

namespace McpSdk.Server
{
    public sealed class ServerBuilder
    {
        public IServer Build()
        {
            throw new NotImplementedException();
        }

        public ServerBuilder WithStdioTransport()
        {
            return this;
        }

        public ServerBuilder WithName(string name)
        {
            return this;
        }

        public ServerBuilder WithVersion(string version)
        {
            return this;
        }

        public ServerBuilder WithTool(IToolFactory toolFactory)
        {
            return this;
        }
    }
}