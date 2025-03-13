using System;
using System.Net;
using System.Threading.Tasks;
using McpSdk.Server;

namespace McpSdk.Adapter.SseServer
{
    public sealed class HttpListenerSseServer : ISseServer
    {
        public event Action<string> MessageReceived;
        
        private readonly HttpListener _listener;
        private Task _listeningTask;

        public HttpListenerSseServer()
        {
            _listener = new HttpListener();
        }
        
        public Task Start()
        {
            _listener.Start();
            _listeningTask = Listen();
            return Task.CompletedTask;
        }

        public Task Send(string json)
        {
            throw new NotImplementedException();
        }

        private Task Listen()
        {
            throw new NotImplementedException();
        }
    }
}