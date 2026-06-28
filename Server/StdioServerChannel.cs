using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// The stdio half of the new transport seam: a dumb duplex pipe over the process's std handles. It
    /// frames JSON-RPC messages as newline-delimited UTF-8 (no BOM) and does nothing else — no ids, no
    /// correlation. Wrap it in a <see cref="JsonRpcPeer"/> to get an <c>ITransport</c>.
    /// </summary>
    public sealed class StdioServerChannel : IMessageChannel
    {
        // UTF-8 with no BOM: the stdio spec mandates UTF-8, and a BOM would corrupt the first frame.
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private TextWriter _standardOut;
        private TextReader _standardIn;
        private CancellationTokenSource _cts;

        public event Action<string> FrameReceived;

        public Task Start(CancellationToken cancellationToken = default)
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

        public Task Stop()
        {
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public async Task Send(JsonRpcFrame frame, CancellationToken cancellationToken = default)
        {
            await _standardOut.WriteLineAsync(JsonRpcFraming.ToSingleLine(frame.Payload)).ConfigureAwait(false);
        }

        private async Task ReadLoop(TextReader standardIn, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await standardIn.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                    break;

                FrameReceived?.Invoke(line);
            }
        }
    }
}
