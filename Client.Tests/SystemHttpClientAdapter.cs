using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using McpSharp.Client;

class SystemHttpClientAdapter : ISseClient
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<SseMessage> _receivedMessages = new();
    private readonly SseMessageReader _sseMessageReader;
    
    public SystemHttpClientAdapter()
    {
        _httpClient = new HttpClient();
        _sseMessageReader = new SseMessageReader(_receivedMessages);
    }
    
    public async Task<IHttpResponse> SendMessage(string url, string jsonBody, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Sending: {jsonBody}");
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new HttpResponseAdapter(response);
    }

    public async Task<ISseMessage> DequeueMessage(CancellationToken cancellationToken)
    {
        SseMessage message;
        while (!_receivedMessages.TryDequeue(out message))
        {
            await Task.Delay(100, cancellationToken);
        }
        return message;
    }
    
    private Task _startListeningTask;

    public async Task Connect(string sseUrl, CancellationToken cancellationToken = default)
    {
        _startListeningTask = StartListening(sseUrl, cancellationToken);
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

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);
                // Continuously read the stream.
                while (!reader.EndOfStream)
                {
                    // Read a line from the stream.
                    var line = await reader.ReadLineAsync(cancellationToken);
                    _sseMessageReader.ProcessLine(line);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }   
    }

    public void Dispose()
    {
        
    }
}

public sealed class SseMessage : ISseMessage
{
    public string? Id { get; set; }
    public string Kind { get; }
    public string? Data { get; set; }

    public SseMessage(string kind)
    {
        Kind = kind;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append("event: ").Append(Kind).Append(' ');
        if (Id != null)
            sb.Append("id: ").Append(Id).Append(' ');
        if (Data != null)
            sb.Append("data: ").Append(Data).Append(' ');
        return sb.ToString();
    }
}

class SseMessageReader
{
    private SseMessage? _currentMessage;
    private readonly ConcurrentQueue<SseMessage> _messages;

    public SseMessageReader(ConcurrentQueue<SseMessage> messages)
    {
        _messages = messages;
    }

    public void ProcessLine(string? line)
    {
        Console.WriteLine($"Processing {line}");
        if (string.IsNullOrEmpty(line))
        {
            if (_currentMessage != null)
            {
                _messages.Enqueue(_currentMessage);
                _currentMessage = null;
            }
            
            return;
        }
     
        if (line.StartsWith("event: "))
        {
            var kind = line.Substring(7);
            _currentMessage = new SseMessage(kind);
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