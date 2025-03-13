using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Server;

namespace McpSdk.Adapter.SseServer
{
    internal sealed class SseConnection : ISseConnection
    {
        public event Action ClientConnected;
        public event Action<string> MessageReceived;
        
        public Task Send(SseEvent sseEvent)
        {
            return    Task.CompletedTask;
        }

        public void Start(Stream outputStream)
        {
            
        }
    }
    
    
    public sealed class HttpListenerSseServer : ISseServer
    {
        private readonly HttpListener _listener;
        private CancellationTokenSource _cts;
        private Task _listeningTask;

        private readonly Dictionary<string, SseConnection> _connectionByMessagePathLookup = new Dictionary<string, SseConnection>();
        private readonly Dictionary<string, SseConnection> _connectionByConnectionPathLookup = new Dictionary<string, SseConnection>();
        
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
        
        public ISseConnection StartListening(string connectionPath, string messagesPath)
        {
            var connection = new SseConnection();
            _connectionByMessagePathLookup.Add(connectionPath, connection);
            _connectionByConnectionPathLookup.Add(messagesPath, connection);
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
                var path = request.Url.AbsolutePath;
                var isGetMethod = method.Equals("GET", StringComparison.OrdinalIgnoreCase);
                var isPostMethod = method.Equals("POST", StringComparison.OrdinalIgnoreCase);
                var hasRequiredHeaders = request.ContentType?.Contains("text/event-stream") ?? false;
                
                if (isGetMethod && hasRequiredHeaders)
                {
                    if (_connectionByConnectionPathLookup.TryGetValue(path, out var connection))
                    {
                        response.ContentType = "text/event-stream";
                        response.Headers.Add("Cache-Control", "no-cache");
                        response.Headers.Add("Connection", "keep-alive");
                        connection.Start(response.OutputStream);
                    }
                }
                else if (isPostMethod)
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                    response.Close();
                }
            }
        }
    }
}