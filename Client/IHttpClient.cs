using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IHttpClient
    {
        Task Connect(string sseEndpoint, CancellationToken cancellationToken = default);
        void Dispose();
        Task<IHttpResponse> PostMessage(string url, string jsonBody, CancellationToken cancellationToken = default);
        Task<string> DequeueMessage(CancellationToken cancellationToken = default);
    }
}