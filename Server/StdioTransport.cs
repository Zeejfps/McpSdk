using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server
{
    public sealed class StdioTransport : JsonRpcTransport
    {
        private TextWriter _standardOut;
    
        public StdioTransport(IJson json) : base(json)
        {
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
        {
            _standardOut = Console.Out;
            ReadStdIn(Console.In);
            return Task.CompletedTask;
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            await _standardOut.WriteLineAsync(requestAsJson).ConfigureAwait(false);
        }
    
        private async Task ReadStdIn(TextReader standardIn)
        {
            string messageAsJson;
            while ((messageAsJson = await standardIn.ReadLineAsync().ConfigureAwait(false)) != null)
                OnMessageReceived(messageAsJson);
        }
    }
}