using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    public sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseClient _sseClient;
        private string _messagesEndpoint;
        private TaskCompletionSource<bool> _startedTcs;

        public SseTransport(ISseClient sseClient, IJson json, ILoggerFactory loggerFactory) : base(json, loggerFactory)
        {
            _sseClient = sseClient;
        }
        
        private void OnSseEventReceived(ISseEvent sseEvent)
        {
            if (sseEvent.Kind == "endpoint")
            {
                _messagesEndpoint = sseEvent.Data;
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
            await _sseClient.Connect(cancellationToken);
            await _startedTcs.Task;
        }

        protected override async Task OnStop(CancellationToken cancellationToken = default)
        {
            _sseClient.EventReceived -= OnSseEventReceived;
            await _sseClient.Disconnect();
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _sseClient.SendMessage(_messagesEndpoint, requestAsJson, cancellationToken);
        }
    }
}