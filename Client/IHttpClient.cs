using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    public interface IHttpClient
    {
        Task<JsonRpcResponse<TResponsePayload>> Post<TResponsePayload>(string endpoint, string requestAsJson, CancellationToken cancellationToken = default) where TResponsePayload : class;
        Task Post(string endpoint, string requestAsJson, CancellationToken cancellationToken = default);
    }
}