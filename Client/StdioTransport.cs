using System;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class StdioTransport : ITransport
    {
        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;
        public Task Connect(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SendNotification(string notification, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IJsonObject> SendRequest(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task SendResponse(int messageId, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}