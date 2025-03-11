using System;
using System.Collections.Generic;
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
        private readonly Dictionary<int, TaskCompletionSource<IJsonObject>> _tscByMessageId = new Dictionary<int, TaskCompletionSource<IJsonObject>>();

        private StreamWriter _standardIn;
        private Process _process;
        private int _nextMessageId;
        private Task _readStdOutTask;
        private Task _readStdErrTask;
        
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

            _standardIn = _process.StandardInput;
            _readStdOutTask = ReadStdOut(_process.StandardOutput);
            _readStdErrTask = ReadStdErr(_process.StandardError);
            
            return Task.CompletedTask;
        }

        private async Task ReadStdOut(StreamReader standardOut)
        {
            string message;
            while ((message = await standardOut.ReadLineAsync()) != null)
            {
                Console.WriteLine($"Received: {message}");
                var response = _json.Parse(message);
                var idProp = response["id"];
                if (idProp == null) 
                    return;
            
                var id = idProp.AsInt();
                if (!_tscByMessageId.TryGetValue(id, out var tsc))
                    return;
            
                _tscByMessageId.Remove(id);
                tsc.TrySetResult(response);
            }
        }
        
        private async Task ReadStdErr(StreamReader standardErr)
        {
            string message;
            while ((message = await standardErr.ReadLineAsync()) != null)
            {
                Console.WriteLine($"{message}");
            }
        }
        
        private Task<IJsonObject> WaitForResponse(int messageId, CancellationToken cancellationToken = default)
        {
            var tsc = new TaskCompletionSource<IJsonObject>(cancellationToken);
            _tscByMessageId[messageId] = tsc;
            return tsc.Task;
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
            var response = await WaitForResponse(id, cancellationToken);
            return ReadResult(response);
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