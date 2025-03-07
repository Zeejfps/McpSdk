using System.Threading;
using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IHttpClient
    {
        Task Connect(string sseEndpoint);
        void Dispose();
        Task<IHttpResponse> PostMessage(string url, string jsonBody, CancellationToken cancellationToken = default);
        Task<string> DequeueMessage(CancellationToken cancellationToken);
    }
}