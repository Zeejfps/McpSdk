using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    public sealed class StdioTransport : JsonRpcTransport
    {
        // UTF-8 with no BOM: the stdio spec mandates UTF-8, and a BOM would corrupt the first frame.
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private TextWriter _standardOut;
        private TextReader _standardIn;
        private CancellationTokenSource _cts;

        public StdioTransport(IJson json, ILoggerFactory loggerFactory) : base(json, loggerFactory)
        {
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
        {
            _cts = new CancellationTokenSource();

            // Bind directly to the raw stdout/stdin handles so framing is independent of the console
            // locale: UTF-8 (no BOM), LF line endings, flushed per message so the peer never stalls.
            _standardOut = new StreamWriter(Console.OpenStandardOutput(), Utf8NoBom)
            {
                AutoFlush = true,
                NewLine = JsonRpcFraming.LineDelimiter.ToString(),
            };
            _standardIn = new StreamReader(Console.OpenStandardInput(), Utf8NoBom);

            // stdout is reserved exclusively for MCP messages. Redirect Console.Out to stderr so any
            // stray Console.Write (e.g. from tool code) can never corrupt the protocol stream.
            Console.SetOut(Console.Error);

            // Fire and forget
            _ = ReadStdIn(_standardIn, _cts.Token);
            
            return Task.CompletedTask;
        }

        protected override Task OnStop(CancellationToken cancellationToken = default)
        {
            // ReadLineAsync cannot be cancelled mid-read; flipping the token stops the loop from
            // dispatching any further messages and unwinds it on the next EOF/line.
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken = default)
        {
            var line = JsonRpcFraming.ToSingleLine(requestAsJson);
            await _standardOut.WriteLineAsync(line).ConfigureAwait(false);
        }

        private async Task ReadStdIn(TextReader standardIn, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Logger.LogDebug("Reading stdin...");
                var messageAsJson = await standardIn
                    .ReadLineAsync()
                    .ConfigureAwait(false);
                
                if (messageAsJson == null)
                    break;
                
                OnMessageReceived(messageAsJson);
            }
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
