using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Client;

namespace McpSharp.Adapter.SseClient
{
    internal sealed class SseClient : ISseClient
    {
        private readonly HttpClient _httpClient;
        private readonly SseMessageReader _sseMessageReader;
    
        private Task _startListeningTask;

        public SseClient()
        {
            _httpClient = new HttpClient();
            _sseMessageReader = new SseMessageReader();
        }
    
        public event Action<ISseEvent> EventReceived
        {
            remove => _sseMessageReader.EventReceived -= value;
            add => _sseMessageReader.EventReceived += value;
        }

        public async Task SendMessage(string url, string jsonBody, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Sending: {jsonBody} to {url}");
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    
        public async Task Connect(string url, CancellationToken cancellationToken = default)
        {
            _startListeningTask = StartListening(url, cancellationToken);
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
                    while (!reader.EndOfStream)
                    {
                        // Read a line from the stream.
                        var line = await reader.ReadLineAsync();
                        _sseMessageReader.ProcessLine(line);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }   
        }
    }

    class SseMessageReader
    {
        private SseEvent? _currentMessage;

        public event Action<ISseEvent>? EventReceived;
    
        public void ProcessLine(string? line)
        {
            //Console.WriteLine($"Processing {line}");
            if (string.IsNullOrEmpty(line))
            {
                if (_currentMessage != null)
                {
                    EventReceived?.Invoke(_currentMessage);
                    _currentMessage = null;
                }
            
                return;
            }
     
            if (line.StartsWith("event: "))
            {
                var kind = line.Substring(7);
                _currentMessage = new SseEvent(kind);
            }
            else if (line.StartsWith("data: "))
            {
                if (_currentMessage == null)
                {
                    throw new Exception("Expected event, got data");
                }
            
                _currentMessage.Data = line.Substring(6);
            }
            else if (line.StartsWith("id:"))
            {
                if (_currentMessage == null)
                {
                    throw new Exception("Expected event, got id");
                }
            
                _currentMessage.Id = line.Substring(2);
            }
        }
    }
}