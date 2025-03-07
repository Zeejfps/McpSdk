using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IHttpClientFactory
    {
        Task<IHttpClient> CreateHttpClient(string sseEndpoint);
    }
}