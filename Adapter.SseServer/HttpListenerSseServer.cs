using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Server;

namespace McpSdk.Adapter.SseServer
{
    public sealed class HttpListenerSseServer : ISseServer
    {
        private readonly HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listeningTask;

        private readonly Dictionary<string, SseChannel> _channelByMessagePathLookup = new Dictionary<string, SseChannel>();
        private readonly Dictionary<string, SseChannel> _channelByConnectionPathLookup = new Dictionary<string, SseChannel>();
        
        public HttpListenerSseServer()
        {
            _listener = new HttpListener();
        }
        
        public Task Start()
        {
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listeningTask = Listen();
            return Task.CompletedTask;
        }
        
        public ISseChannel CreateChannel(string connectionPath, string messagesPath)
        {
            var connection = new SseChannel();
            _channelByMessagePathLookup.Add(connectionPath, connection);
            _channelByConnectionPathLookup.Add(messagesPath, connection);
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
                var hasEventStreamHeaders = request.ContentType?.Contains("text/event-stream") ?? false;
                
                if (isGetMethod && hasEventStreamHeaders)
                {
                    if (_channelByConnectionPathLookup.TryGetValue(path, out var connection))
                    {
                        connection.Establish(response);
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