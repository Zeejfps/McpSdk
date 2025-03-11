using McpSharp.Protocol;

namespace Server;

public sealed class StdioServerTransport : JsonRpcTransport
{
    public StdioServerTransport(IJson json) : base(json)
    {
    }

    protected override Task OnStart(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    protected override Task Send(string requestAsJson, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}