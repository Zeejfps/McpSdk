using System;

namespace McpSdk.Protocol
{
    /// <summary>
    /// Thrown by a request handler (or a params parser) to return a specific JSON-RPC error to the peer.
    /// The server's dispatch loop catches it and replies with <see cref="Code"/> + message, instead of
    /// collapsing every failure into a generic <see cref="ErrorCode.InternalError"/> (-32603). Use it for
    /// caller-facing errors such as malformed params (<see cref="ErrorCode.InvalidParams"/>).
    /// </summary>
    public sealed class McpErrorException : Exception
    {
        public McpErrorException(ErrorCode code, string message) : base(message)
        {
            Code = code;
        }

        /// <summary>The JSON-RPC error code to send back to the peer.</summary>
        public ErrorCode Code { get; }
    }
}
