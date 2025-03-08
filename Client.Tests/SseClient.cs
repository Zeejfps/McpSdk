using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using McpSharp.Client;

internal class SseClient : ISseClient
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<SseEvent> _receivedMessages = new();
    private readonly SseMessageReader _sseMessageReader;

    private Task? _startListeningTask;

    public SseClient()
    {
        _httpClient = new HttpClient();
        _sseMessageReader = new SseMessageReader(_receivedMessages);
    }

    public async Task SendMessage(string url, string jsonBody, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Sending: {jsonBody}");
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        Console.WriteLine($"Message sent to: {url}");
    }

    public async Task<ISseEvent> DequeueEvent(CancellationToken cancellationToken)
    {
        SseEvent message;
        while (!_receivedMessages.TryDequeue(out message))
        {
            await Task.Delay(100, cancellationToken);
        }
        return message;
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

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
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
}

public sealed class SseEvent : ISseEvent
{
    public string? Id { get; set; }
    public string Kind { get; }
    public string? Data { get; set; }

    public SseEvent(string kind)
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
    private SseEvent? _currentMessage;
    private readonly ConcurrentQueue<SseEvent> _messages;

    public SseMessageReader(ConcurrentQueue<SseEvent> messages)
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