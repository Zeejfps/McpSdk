using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal sealed class StdioTransport : JsonRpcTransport
    {
        private TextWriter _standardOut;
        private Task _readStdInTask;
    
        public StdioTransport(IJson json, ILoggerFactory loggerFactory) : base(json, loggerFactory)
        {
        }

        protected override async Task OnStart(CancellationToken cancellationToken = default)
        {
            _standardOut = Console.Out;
            _readStdInTask = ReadStdIn(Console.In);
            await Task.Yield();
        }

        protected override Task OnStop(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            requestAsJson = Regex.Replace(requestAsJson, @"\t|\n|\r", string.Empty);
            await _standardOut.WriteLineAsync(requestAsJson).ConfigureAwait(false);
        }
    
        private async Task ReadStdIn(TextReader standardIn)
        {
            string messageAsJson;
            while ((messageAsJson = await standardIn.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                Logger.LogDebug("Reading stdin...");
                OnMessageReceived(messageAsJson);
            }
            Logger.LogDebug("Null message received");
        }
    }

    public static class StdioTransportServerBuilderExtensions
    {
        public static ServerBuilder WithStdioTransport(this ServerBuilder builder, IJson json)
        {
            var factory = new StdioTransportFactory(json);
            builder.WithTransport(factory);
            return builder;
        }
    }
}