using System;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    public sealed class SseTransport : JsonRpcTransport
    {
        private readonly ISseSession _sseSession;
        
        public SseTransport(
            IJson json,
            ISseSession sseSession,
            ILoggerFactory loggerFactory) : base(json, loggerFactory)
        {
            _sseSession = sseSession;
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
        {
            try
            {
                _sseSession.Send(new SseEvent
                {
                    Kind = "endpoint",
                    Data = _sseSession.Path
                });
                Logger.LogDebug($"Sending Endpoint Event: {_sseSession.Path}");
                
                _sseSession.MessageReceived += OnMessageReceived;
            }
            catch (Exception e)
            {
                if (_sseSession != null)
                {
                    _sseSession.MessageReceived -= OnMessageReceived;
                }
                Console.Error.WriteLine(e);
            }
            
            return Task.CompletedTask;
        }

        protected override async Task OnStop(CancellationToken cancellationToken = default)
        {
            _sseSession.MessageReceived -= OnMessageReceived;
            Logger.LogDebug("Stopping Sse Channel");
            await _sseSession.Close();
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _sseSession.Send(new SseEvent
            {
                Kind = "message",
                Data = $"{requestAsJson}"
            });
        }
    }
}