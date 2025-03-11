using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class StdioTransport : JsonRpcTransport
    {
        
        private readonly string _command;
        private readonly string _arguments;

        private StreamWriter _standardIn;
        private Process _process;
        private Task _readStdOutTask;
        private Task _readStdErrTask;
        
        public StdioTransport(IJson json, string command, string arguments) : base(json)
        {
            _command = command;
            _arguments = arguments;
        }

        protected override Task OnConnect(CancellationToken cancellationToken = default)
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

            _standardIn = _process.StandardInput;
            _readStdOutTask = ReadStdOut(_process.StandardOutput);
            _readStdErrTask = ReadStdErr(_process.StandardError);
            
            return Task.CompletedTask;
        }

        protected override Task Send(string requestAsJson, CancellationToken cancellationToken)
        {
            return _standardIn.WriteLineAsync(requestAsJson);
        }

        private async Task ReadStdOut(StreamReader standardOut)
        {
            string responseAsJson;
            while ((responseAsJson = await standardOut.ReadLineAsync()) != null)
                OnResponseReceived(responseAsJson);
        }
        
        private async Task ReadStdErr(StreamReader standardErr)
        {
            string message;
            while ((message = await standardErr.ReadLineAsync()) != null)
            {
                Console.WriteLine($"{message}");
            }
        }
    }
}