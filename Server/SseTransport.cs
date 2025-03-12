using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class SseTransport : JsonRpcTransport
    {
        public SseTransport(IJson json) : base(json)
        {
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

        protected override Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}