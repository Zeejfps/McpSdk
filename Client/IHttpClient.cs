using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface IHttpClient
    {
        Task<IHttpResponse> Post<TResponsePayload>(string url, string jsonBody, CancellationToken cancellationToken = default) where TResponsePayload : class;
        Task<IHttpResponse> Post(string url, string jsonBody, CancellationToken cancellationToken = default);
    }
}