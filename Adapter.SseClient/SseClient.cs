using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Shared;

namespace McpSdk.Adapter.SseClient
{
    internal sealed class SseClient : ISseClient
    {
        private readonly HttpClient _httpClient;
        private readonly SseMessageReader _sseMessageReader;
        private readonly ILogger _logger;
        
        private Task _startListeningTask;
        private CancellationTokenSource _cts;

        public SseClient(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.Create<SseClient>();
            _httpClient = new HttpClient();
            _sseMessageReader = new SseMessageReader();
        }
    
        public event Action<ISseEvent> EventReceived
        {
            add => _sseMessageReader.EventReceived += value;
            remove => _sseMessageReader.EventReceived -= value;
        }

        public async Task SendMessage(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        public Task Connect(string url, CancellationToken cancellationToken = default)
        {
            _cts = new CancellationTokenSource();
            var linkedTokenSrc = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, cancellationToken);
            _startListeningTask = StartListening(url, linkedTokenSrc.Token);
            return Task.CompletedTask;
        }

        public async Task Disconnect()
        {
            try
            {
                _cts.Cancel();
                await _startListeningTask;
            }
            catch (OperationCanceledException)
            {
                
            }
        }

        private async Task StartListening(string sseUrl, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, sseUrl);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                    using var stream = await response.Content.ReadAsStreamAsync();
                    using var reader = new StreamReader(stream);
                    // Continuously read the stream.
                    while (!cancellationToken.IsCancellationRequested && !reader.EndOfStream)
                    {
                        // Read a line from the stream.
                        var line = await reader.ReadLineAsync();
                        _sseMessageReader.ProcessLine(line);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                }
            }   
        }
    }
}