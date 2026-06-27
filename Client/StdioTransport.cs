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
    public sealed class StdioTransport : JsonRpcTransport
    {
        // UTF-8 with no BOM: the stdio spec mandates UTF-8, and a BOM would corrupt the first frame.
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private readonly string _command;
        private readonly string _arguments;

        private StreamWriter _standardIn;
        private Process _process;
        private CancellationTokenSource _cts;
        private Task _readStdOutTask;
        private Task _readStdErrTask;
        
        public StdioTransport(IJson json, ILoggerFactory loggerFactory, string command, string arguments) : base(json, loggerFactory)
        {
            _command = command;
            _arguments = arguments;
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
                throw new ClientException("Failed to connect to the server.");

            _cts = new CancellationTokenSource();

            // Force UTF-8 (no BOM) + LF on the redirected streams by wrapping the raw base streams
            // ourselves. UTF8Encoding(false) has an empty preamble, so the writer never emits a BOM —
            // and unlike ProcessStartInfo.Standard*Encoding (absent on netstandard2.0) this works on
            // every target framework. AutoFlush is required so each frame reaches the child promptly.
            _standardIn = new StreamWriter(_process.StandardInput.BaseStream, Utf8NoBom)
            {
                AutoFlush = true,
                NewLine = JsonRpcFraming.LineDelimiter.ToString(),
            };
            // StreamReader detects and skips a leading BOM by default, so any BOM the child emits is
            // stripped rather than parsed as part of the first frame.
            var standardOut = new StreamReader(_process.StandardOutput.BaseStream, Utf8NoBom);
            var standardErr = new StreamReader(_process.StandardError.BaseStream, Utf8NoBom);

            _readStdOutTask = ReadStdOut(standardOut, _cts.Token);
            _readStdErrTask = ReadStdErr(standardErr, _cts.Token);

            return Task.CompletedTask;
        }

        protected override async Task OnStop(CancellationToken cancellationToken = default)
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _process?.Kill();
                _cts?.Cancel();
                var waitForReadersTask = Task.WhenAll(_readStdOutTask, _readStdErrTask);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), timeoutCts.Token);
                await Task.WhenAny(waitForReadersTask, timeoutTask).ConfigureAwait(false);
                timeoutCts.Cancel();
            }
            catch (OperationCanceledException)
            {
                
            }
        }

        protected override async Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            var line = JsonRpcFraming.ToSingleLine(requestAsJson);
            await _standardIn.WriteLineAsync(line).ConfigureAwait(false);
        }

        private async Task ReadStdOut(StreamReader standardOut, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var messageAsJson = await standardOut
                    .ReadLineAsync()
                    .ConfigureAwait(false);
                
                if (messageAsJson == null)
                    break;
                
                Logger.LogDebug($"[SERVER-OUT] {messageAsJson}");
                OnMessageReceived(messageAsJson);
            }
        }

        private async Task ReadStdErr(StreamReader standardErr, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var message = await standardErr
                    .ReadLineAsync()
                    .ConfigureAwait(false);
                
                if (message == null)
                    break;

                Logger.LogDebug($"[SERVER-ERR]: {message}");
            }
        }
    }
}