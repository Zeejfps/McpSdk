using System.Text;
using McpSharp.Client;

class SystemHttpClientAdapter : IHttpClient
{
    private readonly IJson _json;
    private readonly HttpClient _httpClient;

    public SystemHttpClientAdapter(IJson json, HttpClient httpClient)
    {
        _json = json;
        _httpClient = httpClient;
    }

    public async Task<IHttpResponse> Post<TResponsePayload>(string url, string jsonBody, CancellationToken cancellationToken = default) where TResponsePayload : class
    {
        Console.WriteLine($"Sending: {jsonBody}");
        var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        Console.WriteLine($"Response: {response.StatusCode}");
        response.EnsureSuccessStatusCode();
        return new HttpResponseAdapter(response);
    }

    public Task<IHttpResponse> Post(string url, string requestAsJson, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}