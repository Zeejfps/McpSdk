using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseServer _server;
        
        public SseTransport(IJson json, ISseServer server) : base(json)
        {
            _server = server;
        }

        protected override async Task OnStart(CancellationToken cancellationToken = default)
        {
            try
            {
                _server.MessageReceived += OnMessageReceived;
                await _server.Start();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                _server.MessageReceived -= OnMessageReceived;
            }
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _server.Send(requestAsJson);
        }
    }
}