using System.Threading.Tasks;

namespace McpSharp.Client
{
    public interface IHttpResponse
    {
        Task<string> ReadContentAsJsonString();
    }
}