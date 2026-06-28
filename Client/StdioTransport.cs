using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Client
{
    /// <summary>
    /// The client stdio transport: spawns the server process and uses its stdin/stdout as the wire,
    /// rendering outbound JSON-RPC messages and parsing inbound ones as newline-delimited UTF-8 (no BOM).
    /// Correlation and dispatch are inherited from <see cref="JsonRpcTransport"/>.
    /// </summary>
    public sealed class StdioTransport : JsonRpcTransport
    {
        // UTF-8 with no BOM: the stdio spec mandates UTF-8, and a BOM would corrupt the first frame.
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly string _command;
        private readonly string _arguments;
        private readonly IJson _json;

        private StreamWriter _standardIn;
        private Process _process;
        private CancellationTokenSource _cts;
        private Task _readStdOutTask;
        private Task _readStdErrTask;

        public StdioTransport(string command, string arguments, IJson json, ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
            _command = command;
            _arguments = arguments;
            _json = json;
        }

        protected override Task OnStart(CancellationToken cancellationToken = default)
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
            if (_process == null)
                throw new ClientException("Failed to start the server process.");

            _cts = new CancellationTokenSource();

            // Force UTF-8 (no BOM) + LF on the redirected streams. AutoFlush so each frame reaches the
            // child promptly. StreamReader strips a leading BOM by default.
            _standardIn = new StreamWriter(_process.StandardInput.BaseStream, Utf8NoBom)
            {
                AutoFlush = true,
                NewLine = JsonRpcFraming.LineDelimiter.ToString(),
            };
            var standardOut = new StreamReader(_process.StandardOutput.BaseStream, Utf8NoBom);
            var standardErr = new StreamReader(_process.StandardError.BaseStream, Utf8NoBom);

            _readStdOutTask = ReadStdOut(standardOut, _cts.Token);
            _readStdErrTask = ReadStdErr(standardErr, _cts.Token);

            return Task.CompletedTask;
        }

        protected override async Task OnStop()
        {
            try
            {
                _process?.Kill();
                _cts?.Cancel();
                var readers = Task.WhenAll(_readStdOutTask ?? Task.CompletedTask, _readStdErrTask ?? Task.CompletedTask);
                await Task.WhenAny(readers, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        protected override async Task SendMessage(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            await _standardIn.WriteLineAsync(JsonRpcFraming.ToSingleLine(_json.Stringify(message.WriteMembers))).ConfigureAwait(false);
        }

        private async Task ReadStdOut(StreamReader standardOut, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await standardOut.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                    break;

                Logger.LogDebug($"[SERVER-OUT] {line}");
                if (JsonRpcMessage.TryParse(_json, line, out var message))
                    OnMessageReceived(message);
            }
        }

        private async Task ReadStdErr(StreamReader standardErr, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await standardErr.ReadLineAsync().ConfigureAwait(false);
                if (message == null)
                    break;

                Logger.LogDebug($"[SERVER-ERR] {message}");
            }
        }
    }
}
