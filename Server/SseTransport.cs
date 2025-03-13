using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseServer _sseServer;
        
        private string _sessionId;
        private ISseChannel _sseChannel;
        
        public SseTransport(IJson json, ISseServer sseServer) : base(json)
        {
            _sseServer = sseServer;
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
        {
            try
            {
                _sessionId = Guid.NewGuid().ToString("N");
                _sseChannel = _sseServer.CreateChannel("/sse", $"/messages?{_sessionId}");
                _sseChannel.ClientConnected += OnClientConnected;
                _sseChannel.MessageReceived += OnMessageReceived;
            }
            catch (Exception e)
            {
                if (_sseChannel != null)
                {
                    _sseChannel.ClientConnected -= OnClientConnected;
                    _sseChannel.MessageReceived -= OnMessageReceived;
                }
                Console.Error.WriteLine(e);
            }
            
            return Task.CompletedTask;
        }

        private void OnClientConnected()
        {
            _sseChannel.Send(new SseEvent
            {
                Kind = "endpoint",
                Data = $"/messages?{_sessionId}"
            });
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _sseChannel.Send(new SseEvent
            {
                Kind = "message",
                Data = $"{requestAsJson}"
            });
        }
    }
}