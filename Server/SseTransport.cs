using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseServer _sseServer;
        private readonly string _connectionEndpoint;
        private readonly string _messagesEndpoint;

        private string _sessionId;
        private ISseChannel _sseChannel;
        
        public SseTransport(
            IJson json,
            ISseServer sseServer,
            ILoggerFactory loggerFactory,
            string connectionEndpoint,
            string messagesEndpoint) : base(json, loggerFactory)
        {
            _sseServer = sseServer;
            _connectionEndpoint = connectionEndpoint;
            _messagesEndpoint = messagesEndpoint;
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
        {
            try
            {
                _sessionId = Guid.NewGuid().ToString("N");
                _sseChannel = _sseServer.CreateChannel(_connectionEndpoint, $"{_messagesEndpoint}?{_sessionId}");
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
                Data = $"{_messagesEndpoint}?{_sessionId}"
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