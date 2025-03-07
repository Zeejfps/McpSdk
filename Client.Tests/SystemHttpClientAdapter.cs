using System.Text;
using McpSharp.Client;

class SystemHttpClientAdapter : IHttpClient
{
    private readonly HttpClient _httpClient;

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

    public void Dispose()
    {
        
    }
}