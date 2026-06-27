using System.Threading.Tasks;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    /// <summary>
    /// Handles server→client <c>elicitation/create</c> requests. The supported-mode flags drive the
    /// <c>elicitation</c> capability advertised during initialization; a request whose mode is not
    /// supported is rejected by the client with an Invalid Params error before reaching <see cref="Elicit"/>.
    /// </summary>
    public interface IElicitationController
    {
        bool SupportsFormMode { get; }
        bool SupportsUrlMode { get; }
        Task<ElicitResult> Elicit(ElicitRequest request);
    }
}
