using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseConnection _connection;
        
        public SseTransport(IJson json, ISseConnection connection) : base(json)
        {
            _connection = connection;
        }

        protected override async Task OnStart(CancellationToken cancellationToken = default)
        {
            try
            {
                _connection.MessageReceived += OnMessageReceived;
                await _connection.Start();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                _connection.MessageReceived -= OnMessageReceived;
            }
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _connection.Send(requestAsJson);
        }
    }
}