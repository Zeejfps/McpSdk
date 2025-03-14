using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Server;
using McpSdk.Shared;

namespace McpSdk.Adapter.SseServer
{
    public sealed class HttpListenerSseServer
    {
        public event Action<ISseSession> SessionStarted; 
        
        private readonly HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listeningTask;

        private readonly Dictionary<string, SseSession> _sessionByPathLookup = new Dictionary<string, SseSession>();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly string _messagesEndpoint;
        
        public HttpListenerSseServer(string connectionEndpoint, string messagesEndpoint, ILoggerFactory loggerFactory)
        {
            ConnectionPath = connectionEndpoint;
            _messagesEndpoint = messagesEndpoint;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.Create<HttpListenerSseServer>();
            _listener = new HttpListener();
        }
        
        public async Task Start()
        {
            _listener.Prefixes.Add("http://localhost:3000/");
            _listener.Start();
            _cts = new CancellationTokenSource();
            await Listen();
        }

        public Task Stop()
        {
            _cts.Cancel();
            _cts.Dispose();
            _listener.Stop();
            _listeningTask = null;
            return Task.CompletedTask;
        }

        public string ConnectionPath { get; private set; }

        private async Task Listen()
        {
            while (!_cts.IsCancellationRequested)
            {
                var httpContext = await _listener.GetContextAsync();
                var request = httpContext.Request;
                var response = httpContext.Response;
                var method = request.HttpMethod;
                var path = request.Url.PathAndQuery;
                var isConnectionPath = path.Equals(ConnectionPath, StringComparison.OrdinalIgnoreCase);
                var isGetMethod = method.Equals("GET", StringComparison.OrdinalIgnoreCase);
                var isPostMethod = method.Equals("POST", StringComparison.OrdinalIgnoreCase);
                var hasEventStreamHeaders = request.AcceptTypes?.Contains("text/event-stream") ?? false;
                _logger.LogDebug($"Client connected: {method}, {path}");

                if (isGetMethod && hasEventStreamHeaders && isConnectionPath)
                {
                    var sessionId = Guid.NewGuid().ToString("N");
                    var sessionPath = $"{_messagesEndpoint}?{sessionId}";
                    var session = new SseSession(_loggerFactory, sessionPath, response);
                    _sessionByPathLookup.Add(sessionPath, session);
                    SessionStarted?.Invoke(session);
                }
                else if (isPostMethod)
                {
                    if (_sessionByPathLookup.TryGetValue(path, out var session))
                    {
                        session.HandlePostMessage(request, response);
                    }
                }
            }
        }
    }
}