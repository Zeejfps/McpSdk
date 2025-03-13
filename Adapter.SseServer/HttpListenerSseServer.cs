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
        private readonly HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listeningTask;

        private readonly Dictionary<string, SseChannel> _channelByMessagePathLookup = new Dictionary<string, SseChannel>();
        private readonly Dictionary<string, SseChannel> _channelByConnectionPathLookup = new Dictionary<string, SseChannel>();
        private readonly ILoggerFactory _loggerFactory;
        
        public HttpListenerSseServer(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _listener = new HttpListener();
        }
        
        public async Task Start()
        {
            _listener.Prefixes.Add("http://localhost:3000/");
            _listener.Start();
            _cts = new CancellationTokenSource();
            await Listen();
        }
        
        public ISseChannel CreateChannel(string connectionPath, string messagesPath)
        {
            var connection = new SseChannel(_loggerFactory);
            _channelByConnectionPathLookup.Add(connectionPath, connection);
            _channelByMessagePathLookup.Add(messagesPath, connection);
            return connection;
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
                var isGetMethod = method.Equals("GET", StringComparison.OrdinalIgnoreCase);
                var isPostMethod = method.Equals("POST", StringComparison.OrdinalIgnoreCase);
                var hasEventStreamHeaders = request.AcceptTypes?.Contains("text/event-stream") ?? false;
                
                if (isGetMethod && hasEventStreamHeaders)
                {
                    if (_channelByConnectionPathLookup.TryGetValue(path, out var connection))
                    {
                        connection.Open(response);
                    }
                }
                else if (isPostMethod)
                {
                    if (_channelByMessagePathLookup.TryGetValue(path, out var connection))
                    {
                        connection.HandlePostMessage(request, response);
                    }
                }
            }
        }
    }
}