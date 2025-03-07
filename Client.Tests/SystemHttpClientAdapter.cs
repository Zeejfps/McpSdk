using System.Collections.Concurrent;
using System.Text;
using McpSharp.Client;

class SystemHttpClientAdapter : IHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<string> _receivedMessages = new();

    public SystemHttpClientAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
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

    public Task<string> DequeueMessage(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task Connect(string sseUrl, CancellationToken cancellationToken = default)
    {
        var client = _httpClient;
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");

        var response = await client.GetAsync(sseUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using (var stream = await response.Content.ReadAsStreamAsync())
        using (var reader = new StreamReader(stream))
        {
            string line;
            while ((line = await reader.ReadLineAsync()) != null && !cancellationToken.IsCancellationRequested)
            {
                _receivedMessages.Enqueue(line);
            }
        }
    }

    public void Dispose()
    {
        
    }
}