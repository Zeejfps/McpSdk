using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    internal sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseClient _sseClient;
        private readonly string _host;
        private readonly string _connectionUrl;

        private string _messagesUrl;
        private TaskCompletionSource<bool> _startedTcs;

        public SseTransport(ISseClient sseClient, IJson json, ILoggerFactory loggerFactory, string host) : base(json, loggerFactory)
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
                _startedTcs.TrySetResult(true);
            }
            else if (sseEvent.Kind == "message")
            {
                OnMessageReceived(sseEvent.Data);
            }
        }

        protected override async Task OnStart(CancellationToken cancellationToken = default)
        {
            _startedTcs = new TaskCompletionSource<bool>();
            _sseClient.EventReceived += OnSseEventReceived;
            await _sseClient.Connect(_connectionUrl, cancellationToken);
            await _startedTcs.Task;
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _sseClient.SendMessage(_messagesUrl, requestAsJson, cancellationToken);
        }
    }
}