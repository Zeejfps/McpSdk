#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Shutdown by end of input: the spec makes closing the server's stdin the primary graceful-shutdown
    /// signal, so a stdio server must answer what it was asked and then exit on its own. This drives the
    /// test assembly in <c>stdio-server</c> mode over a real pipe, closes stdin, and waits for the process
    /// to go away — the regression guard for a read loop that swallowed EOF and hung forever.
    /// </summary>
    public sealed class StdioShutdownTests : ConformanceSuite
    {
        private const int ExitTimeoutMs = 15000;

        public StdioShutdownTests(TestReport report) : base(report) { }

        public override string Title => "Stdio shutdown (stdin EOF)";

        public override async Task Run()
        {
            await Test("closing stdin exits the server after its last response", ExitsOnStdinClose);
            await Test("WaitForShutdown completes when the transport closes", WaitForShutdownCompletesOnClose);
        }

        private async Task ExitsOnStdinClose()
        {
            var (command, arguments) = ResolveStdioServerCommand();
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process == null)
                throw new InvalidOperationException("failed to start the stdio server process");

            var lines = new List<string>();
            var collected = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null)
                    return;
                lock (lines)
                {
                    lines.Add(e.Data);
                    if (lines.Count == 2)
                        collected.TrySetResult(true);
                }
            };
            process.BeginOutputReadLine();
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();

            try
            {
                var parameters = new InitializeRequest(
                    ProtocolVersion.Latest,
                    new ClientCapabilitiesModel(),
                    new ClientInfo("Stdio Shutdown Client", "1.0.0"));
                var initialize = new JsonRpcRequest(new RequestId(1), "initialize", parameters.WriteMembers);

                await process.StandardInput.WriteLineAsync(Json.Stringify(initialize.WriteMembers));
                await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}");
                await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"tools/list\"}");
                await process.StandardInput.FlushAsync();

                await WithTimeout(collected.Task, ExitTimeoutMs, "two responses on stdout");

                // The client's half of the spec's shutdown sequence: close stdin, then wait.
                process.StandardInput.Close();

                var exited = await Task.Run(() => process.WaitForExit(ExitTimeoutMs));
                Assert(exited, $"the server exited within {ExitTimeoutMs}ms of stdin closing");

                if (!exited)
                    return;

                Assert(process.ExitCode == 0, $"the server exited cleanly (exit code {process.ExitCode})");

                // Both answers were already on the wire before it went away: EOF ends the session, it
                // does not cut a response short.
                List<string> received;
                lock (lines)
                    received = new List<string>(lines);

                Assert(received.Count == 2, $"both responses arrived before exit (got {received.Count})");
                foreach (var line in received)
                {
                    var parsed = Json.Parse(line);
                    Assert(parsed["result"] != null, $"response is a complete JSON-RPC result: {Truncate(line)}");
                }
            }
            finally
            {
                try
                {
                    if (!process.HasExited)
                        process.Kill();
                }
                catch { /* best-effort cleanup of the child process */ }
                process.Dispose();
            }
        }

        private async Task WaitForShutdownCompletesOnClose()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await Handshake(clientEnd);

            var shutdown = server.WaitForShutdown();
            Assert(!shutdown.IsCompleted, "WaitForShutdown keeps running while the connection is up");

            serverEnd.Close();

            await WithTimeout(shutdown, 3000, "WaitForShutdown after the transport closed");
            Assert(true, "WaitForShutdown completed once the transport reported the close");

            await server.Stop();
        }

        private static string Truncate(string line) =>
            line.Length <= 80 ? line : line.Substring(0, 80) + "...";
    }
}
