#nullable disable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Phase B conformance: stdio framing correctness. Confirms outgoing messages are collapsed to a
    /// single newline-delimited line without mangling escaped content, that JSON-RPC batches (removed
    /// in 2025-06-18) are rejected rather than processed, and that a real client speaks to a real
    /// server spawned as a child process over stdio.
    /// </summary>
    public static partial class ConformanceTests
    {
        // -- Framing (pure) ------------------------------------------------------------------

        private static Task FramingStripsEmbeddedControl()
        {
            const string dirty = "{\"a\":\t1,\n\"b\":2}\r\n";
            var line = JsonRpcFraming.ToSingleLine(dirty);

            AssertEqual("{\"a\":1,\"b\":2}", line, "embedded CR/LF/TAB stripped");
            Assert(!line.Contains('\n') && !line.Contains('\r') && !line.Contains('\t'),
                "no raw control characters remain in the frame");
            return Task.CompletedTask;
        }

        private static Task FramingPreservesEscapedNewlines()
        {
            // A newline inside a string value serializes as the two-char escape \n, which framing must
            // leave intact — only raw control characters are stripped.
            var raw = Json.Stringify(w => w.Write("text", "line1\nline2"));
            var line = JsonRpcFraming.ToSingleLine(raw);

            AssertEqual(raw, line, "escaped-newline payload is unchanged by framing");
            Assert(!line.Contains('\n') && !line.Contains('\r') && !line.Contains('\t'),
                "frame is a single physical line");
            AssertEqual("line1\nline2", Json.Parse(line)["text"].AsString(),
                "string value survives the framing round-trip");
            return Task.CompletedTask;
        }

        private static Task BatchDetection()
        {
            Assert(JsonRpcFraming.IsBatch("[{\"jsonrpc\":\"2.0\"}]"), "a top-level array is a batch");
            Assert(JsonRpcFraming.IsBatch("  \t [ ]"), "leading whitespace before '[' is still a batch");
            Assert(!JsonRpcFraming.IsBatch("{\"jsonrpc\":\"2.0\"}"), "a top-level object is not a batch");
            Assert(!JsonRpcFraming.IsBatch("   {}"), "leading whitespace before '{' is not a batch");
            Assert(!JsonRpcFraming.IsBatch(""), "an empty message is not a batch");
            return Task.CompletedTask;
        }

        // -- Batch rejection (in-process) ----------------------------------------------------

        private static async Task BatchRejectionRoundTrip()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            var batch =
                "[{\"jsonrpc\":\"2.0\",\"id\":101,\"method\":\"tools/list\",\"params\":{}}," +
                "{\"jsonrpc\":\"2.0\",\"id\":102,\"method\":\"tools/list\",\"params\":{}}]";
            await clientEnd.SendRaw(batch);

            // A valid single request afterwards proves the message pump survived the batch.
            var valid = await clientEnd.SendRequest("tools/list", _ => { });
            Assert(valid.IsOk, "a valid request after a batch is still answered");

            // Let any (erroneous) batch handling settle, then confirm the batch produced no output.
            await Task.Delay(150);
            var sent = Snapshot(serverEnd.Sent).Count;
            Assert(sent == 1, $"batch produced no responses; server sent only the one valid reply (was {sent})");
        }

        // -- Real subprocess round-trip ------------------------------------------------------

        private static async Task StdioRoundTrip()
        {
            var (command, arguments) = ResolveStdioServerCommand();
            var json = new NewtonsoftJson();
            var transport = new JsonRpcPeer(new StdioClientChannel(command, arguments, json, Loggers), Loggers);
            var client = new ClientBuilder()
                .WithName("Stdio Conf Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(transport))
                .Build();

            try
            {
                await WithTimeout(client.Connect(), 30000, "connect");
                Assert(client.IsConnected, "client connected to a real stdio child process");

                var tools = await WithTimeout(client.ListTools(), 15000, "tools/list");
                Assert(tools.Tools.Any(t => t.Name == "get-forecast"), "tools/list round-trips over real stdio");

                var callArgs = json.Object(w =>
                {
                    w.Write("latitude", 47.6062);
                    w.Write("longitude", -122.3321);
                    w.Write("testBool", true);
                    w.Write("testArray", new[] { "alpha", "beta" });
                });
                var result = await WithTimeout(
                    client.CallTool(new CallToolRequest("get-forecast", callArgs)), 15000, "tools/call");

                Assert(result.IsError != true, "tools/call did not report an error");
                var text = result.Content.OfType<TextContent>().FirstOrDefault()?.Text;
                Assert(text != null && text.Contains("47.6062"),
                    $"tools/call result survived the stdio round-trip (got '{text}')");
            }
            finally
            {
                try { await transport.Stop(); }
                catch { /* best-effort cleanup of the child process */ }
            }
        }

        // -- Helpers -------------------------------------------------------------------------

        /// <summary>
        /// Resolves how to launch this same test assembly in <c>stdio-server</c> mode. Prefers the
        /// platform apphost next to the entry dll; falls back to the dotnet muxer.
        /// </summary>
        private static (string command, string arguments) ResolveStdioServerCommand()
        {
            var entryDll = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrEmpty(entryDll))
                entryDll = Path.Combine(AppContext.BaseDirectory, "McpSdk.Server.Tests.dll");

            var appHost = entryDll.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? entryDll.Substring(0, entryDll.Length - 4)
                : entryDll;
            if (OperatingSystem.IsWindows())
                appHost += ".exe";

            if (File.Exists(appHost))
                return (appHost, "stdio-server");

            return ("dotnet", $"\"{entryDll}\" stdio-server");
        }

        private static async Task WithTimeout(Task task, int ms, string what)
        {
            if (await Task.WhenAny(task, Task.Delay(ms)) != task)
                throw new TimeoutException($"stdio {what} timed out after {ms}ms");
            await task;
        }

        private static async Task<T> WithTimeout<T>(Task<T> task, int ms, string what)
        {
            if (await Task.WhenAny(task, Task.Delay(ms)) != task)
                throw new TimeoutException($"stdio {what} timed out after {ms}ms");
            return await task;
        }
    }
}
