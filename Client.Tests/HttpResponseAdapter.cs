using McpSharp.Client;

class HttpResponseAdapter : IHttpResponse
{
    private readonly HttpResponseMessage _response;
    
    public HttpResponseAdapter(HttpResponseMessage response)
    {
        _response = response;
    }

    public Task<string> ReadContentAsJsonString()
    {
        return _response.Content.ReadAsStringAsync();
    }
}