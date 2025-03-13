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
        private ISseConnection _sseConnection;
        
        public SseTransport(IJson json, ISseServer sseServer) : base(json)
        {
            _sseServer = sseServer;
        }

        protected override async Task OnStart(CancellationToken cancellationToken = default)
        {
            try
            {
                _sessionId = Guid.NewGuid().ToString("N");
                _sseConnection = _sseServer.StartListening("/sse", $"/messages?{_sessionId}");
                _sseConnection.ClientConnected += OnClientConnected;
                _sseConnection.MessageReceived += OnMessageReceived;
            }
            catch (Exception e)
            {
                if (_sseConnection != null)
                {
                    _sseConnection.ClientConnected -= OnClientConnected;
                    _sseConnection.MessageReceived -= OnMessageReceived;
                }
                Console.Error.WriteLine(e);
            }
        }

        private void OnClientConnected()
        {
            _sseConnection.Send(new SseEvent
            {
                Kind = "endpoint",
                Data = $"/messages?{_sessionId}"
            });
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _sseConnection.Send(new SseEvent
            {
                Kind = "message",
                Data = $"{requestAsJson}"
            });
        }
    }
}