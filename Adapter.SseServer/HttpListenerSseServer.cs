using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Server;

namespace McpSdk.Adapter.SseServer
{
    public sealed class HttpListenerSseServer : ISseConnection
    {
        public event Action<string> MessageReceived;
        
        private readonly HttpListener _listener;
        
        private CancellationTokenSource _cts;
        private Task _listeningTask;

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

        public Task Send(string json)
        {
            throw new NotImplementedException();
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
                var isSseEndpoint = path.Equals("/sse", StringComparison.OrdinalIgnoreCase);
                var isPostMethod = method.Equals("POST", StringComparison.OrdinalIgnoreCase);
                var hasRequiredHeaders = request.ContentType?.Contains("text/event-stream") ?? false;

                if (isGetMethod && isSseEndpoint && hasRequiredHeaders)
                {
                    response.ContentType = "text/event-stream";
                    response.Headers.Add("Cache-Control", "no-cache");
                    response.Headers.Add("Connection", "keep-alive");
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