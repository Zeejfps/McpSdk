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
    public sealed class HttpListenerSseServer : ISseServer
    {
        public event Action ClientConnected; 
        
        private readonly HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listeningTask;

        private readonly Dictionary<string, SseChannel> _channelsByMessagePath = new Dictionary<string, SseChannel>();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        
        public HttpListenerSseServer(string connectionEndpoint, ILoggerFactory loggerFactory)
        {
            ConnectionPath = connectionEndpoint;
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

        public ISseChannel CreateChannel(string messagesPath)
        {
            if (!_channelsByMessagePath.TryGetValue(messagesPath, out var channel))
            { 
                channel = new SseChannel(_loggerFactory);
                _channelsByMessagePath[messagesPath] = channel;
            }
            
            return channel;
        }

        public void DestroyChannel(string messagesPath)
        {
            _channelsByMessagePath.Remove(messagesPath);
        }

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
                
                if (isGetMethod && hasEventStreamHeaders && isConnectionPath)
                {
                    ClientConnected?.Invoke();
                }
                else if (isPostMethod)
                {
                    if (_channelsByMessagePath.TryGetValue(path, out var connection))
                    {
                        connection.HandlePostMessage(request, response);
                    }
                }
            }
        }
    }
}