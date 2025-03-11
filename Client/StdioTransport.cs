using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class StdioTransport : ITransport
    {
        private const string JsonRpcVersion = "2.0";
        
        private readonly string _command;
        private readonly string _arguments;
        private readonly IJson _json;
        
        private StreamReader _standardOut;
        private StreamWriter _standardIn;
        private Process _process;
        private int _nextMessageId;

        public StdioTransport(IJson json, string command, string arguments)
        {
            _json = json;
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
            if (_process == null)
                throw new ClientException("Failed to connect to the server.");

            _standardOut = _process.StandardOutput;
            _standardIn = _process.StandardInput;
            
            return Task.CompletedTask;
        }

        public async Task SendNotification(string notification, CancellationToken cancellationToken = default)
        {
            var requestAsJson = _json.Stringify(request =>
            {
                request.Write("jsonrpc", JsonRpcVersion);
                request.Write("method", notification);
            });
            await _standardIn.WriteLineAsync(requestAsJson);
        }

        public async Task<IJsonObject> SendRequest(string method, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            var id = NextRequestId();
            var request = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", id);
                req.Write("method", method);
                req.Write("params", payload);
            });

            await _standardIn.WriteLineAsync(request);
            var responseAsJson = await _standardOut.ReadLineAsync();
            return ReadResult(_json.Parse(responseAsJson));
        }

        public async Task SendResponse(int messageId, Action<IJsonWriter> payload, CancellationToken cancellationToken = default)
        {
            var response = _json.Stringify(req =>
            {
                req.Write("jsonrpc", JsonRpcVersion);
                req.Write("id", messageId);
                req.Write("result", payload);
            });
            await _standardIn.WriteLineAsync(response);
        }

        private IJsonObject ReadResult(IJsonObject response)
        {
            var errorProp = response["error"];
            if (errorProp != null)
            {
                var errorObj = errorProp.AsObject();
                var code = errorObj["code"].AsInt();
                var message = errorObj["message"].AsString();
                throw new ClientException($"Error ({code}): {message}");
            }
            return response["result"].AsObject();
        }

        private int NextRequestId()
        {
            return Interlocked.Increment(ref _nextMessageId);
        }
    }
}