using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseClient _sseClient;
        private readonly string _host;
        private readonly string _connectionUrl;

        private string _messagesUrl;

        public SseTransport(ISseClient sseClient, IJson json, string host) : base(json)
        {
            _sseClient = sseClient;
            _host = host;
            _connectionUrl = $"{host}/sse";
            _messagesUrl = $"{host}/messages";
        }
        
        private void OnSseEventReceived(ISseEvent sseEvent)
        {
            if (sseEvent.Kind == "endpoint")
            {
                _messagesUrl = $"{_host}{sseEvent.Data}";
            }
            else if (sseEvent.Kind == "message")
            {
                OnResponseReceived(sseEvent.Data);
            }
        }

        protected override async Task OnConnect(CancellationToken cancellationToken = default)
        {
            _sseClient.EventReceived += OnSseEventReceived;
            await _sseClient.Connect(_connectionUrl, cancellationToken);
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);
        }
    }
}