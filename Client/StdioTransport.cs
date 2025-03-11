using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class StdioTransport : ITransport
    {
        private readonly string _command;
        private readonly string _arguments;
        
        private Process _process;

        public StdioTransport(string command, string arguments)
        {
            _command = command;
            _arguments = arguments;
        }

        public event RequestReceivedCallback RequestReceived;
        public event NotificationReceivedCallback NotificationReceived;
        public Task Connect(CancellationToken cancellationToken = default)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _command,
                Arguments = _arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            
            _process = Process.Start(processStartInfo);
            
            return Task.CompletedTask;
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