using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// The server stdio transport: the wire boundary over the process's std handles, framing JSON-RPC
    /// messages as newline-delimited UTF-8 (no BOM). Correlation and dispatch are inherited from
    /// <see cref="JsonRpcTransport"/>.
    /// </summary>
    public sealed class StdioTransport : JsonRpcTransport
    {
        // UTF-8 with no BOM: the stdio spec mandates UTF-8, and a BOM would corrupt the first frame.
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly IJson _json;

        private TextWriter _standardOut;
        private TextReader _standardIn;
        private CancellationTokenSource _cts;

        public StdioTransport(IJson json, ILoggerFactory loggerFactory) : base(loggerFactory)
        {
            _json = json;
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
        {
            _cts = new CancellationTokenSource();

            // Bind directly to the raw stdout/stdin handles: UTF-8 (no BOM), LF line endings, flushed
            // per message so the peer never stalls.
            _standardOut = new StreamWriter(Console.OpenStandardOutput(), Utf8NoBom)
            {
                AutoFlush = true,
                NewLine = JsonRpcFraming.LineDelimiter.ToString(),
            };
            _standardIn = new StreamReader(Console.OpenStandardInput(), Utf8NoBom);

            // stdout is reserved for MCP frames; redirect Console.Out to stderr so stray writes (e.g.
            // from tool code) can never corrupt the protocol stream.
            Console.SetOut(Console.Error);

            _ = ReadLoop(_standardIn, _cts.Token);
            return Task.CompletedTask;
        }

        protected override Task OnStop()
        {
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        protected override async Task SendMessage(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            await _standardOut.WriteLineAsync(JsonRpcFraming.ToSingleLine(_json.Stringify(message.WriteMembers))).ConfigureAwait(false);
        }

        private async Task ReadLoop(TextReader standardIn, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await standardIn.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                    break;

                if (JsonRpcMessage.TryParse(_json, line, out var message))
                    OnMessageReceived(message);
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
