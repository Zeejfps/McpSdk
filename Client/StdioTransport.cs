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

            // UTF-8 (no BOM) for the redirected streams. Not available on netstandard2.0, which falls
            // back to the platform's console encoding.
#if !NETSTANDARD2_0
            processStartInfo.StandardInputEncoding = Utf8NoBom;
            processStartInfo.StandardOutputEncoding = Utf8NoBom;
            processStartInfo.StandardErrorEncoding = Utf8NoBom;
#endif

            _process = Process.Start(processStartInfo);
            if (_process == null)
                throw new ClientException("Failed to connect to the server.");

            _cts = new CancellationTokenSource();
            _standardIn = _process.StandardInput;
            // Delimit frames with LF (never the platform's CRLF) to match the stdio framing contract.
            _standardIn.NewLine = JsonRpcFraming.LineDelimiter.ToString();
            _readStdOutTask = ReadStdOut(_process.StandardOutput);
            _readStdErrTask = ReadStdErr(_process.StandardError);
            
            return Task.CompletedTask;
        }

        protected override async Task OnStop(CancellationToken cancellationToken = default)
        {
            try
            {
                _process.Kill();
                _cts?.Cancel();
                await Task.WhenAll(_readStdOutTask, _readStdErrTask);
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

        private async Task ReadStdOut(StreamReader standardOut)
        {
            string messageAsJson;
            while (!_cts.IsCancellationRequested && (messageAsJson = await standardOut.ReadLineAsync()) != null)
            {
                Logger.LogDebug($"[SERVER-OUT] {messageAsJson}");
                OnMessageReceived(messageAsJson);
            }
        }
        
        private async Task ReadStdErr(StreamReader standardErr)
        {
            string message;
            while (!_cts.IsCancellationRequested && (message = await standardErr.ReadLineAsync()) != null)
            {
                Logger.LogDebug($"[SERVER-ERR]: {message}");
            }
        }
    }
}