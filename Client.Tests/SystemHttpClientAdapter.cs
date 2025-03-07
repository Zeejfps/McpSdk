using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using McpSharp.Client;

class SystemHttpClientAdapter : ISseClient
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<string> _receivedMessages = new();

    public SystemHttpClientAdapter()
    {
        _httpClient = new HttpClient();
    }
    
    public async Task<IHttpResponse> PostMessage(string url, string jsonBody, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Sending: {jsonBody}");
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        Console.WriteLine($"Response: {response.StatusCode}");
        response.EnsureSuccessStatusCode();
        return new HttpResponseAdapter(response);
    }

    public async Task<string> DequeueMessage(CancellationToken cancellationToken)
    {
        string message;
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
                    if (string.IsNullOrEmpty(line))
                        continue;
                    
                    Console.WriteLine($"{line}");
                    _receivedMessages.Enqueue(line);
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