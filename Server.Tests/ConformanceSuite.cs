#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Accumulates pass/fail counts and prints a live PASS/FAIL log for an entire conformance run. A
    /// single instance is shared across every <see cref="ConformanceSuite"/> so the final tally spans
    /// all features.
    /// </summary>
    public sealed class TestReport
    {
        public int Passed { get; private set; }
        public int Failed { get; private set; }

        /// <summary>Prints the section header for the suite that is about to run.</summary>
        public void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {title} ===");
        }

        /// <summary>Runs one named test, turning any thrown exception into a single failure.</summary>
        public async Task Test(string name, Func<Task> body)
        {
            Console.WriteLine($"[{name}]");
            try
            {
                await body();
            }
            catch (Exception ex)
            {
                Failed++;
                Console.WriteLine($"  FAIL: threw {ex.GetType().Name}: {ex.Message}");
            }
        }

        public void Assert(bool condition, string name)
        {
            if (condition)
            {
                Passed++;
                Console.WriteLine($"  PASS: {name}");
            }
            else
            {
                Failed++;
                Console.WriteLine($"  FAIL: {name}");
            }
        }

        public void AssertEqual(string expected, string actual, string name)
            => Assert(expected == actual, $"{name} (expected '{expected}', got '{actual}')");

        /// <summary>Prints the final tally and returns the failure count (used as the process exit code).</summary>
        public int Summarize()
        {
            Console.WriteLine();
            Console.WriteLine($"=== {Passed} passed, {Failed} failed ===");
            return Failed;
        }
    }

    /// <summary>
    /// Base class for a feature-scoped conformance suite. Each concrete suite covers one MCP feature
    /// (named by <see cref="Title"/>) and registers its tests in <see cref="Run"/>. This base holds the
    /// shared JSON adapter, logger factory, assertion sink, and the transport/handshake helpers that more
    /// than one suite needs; helpers used by a single suite live with that suite.
    /// </summary>
    public abstract class ConformanceSuite
    {
        protected static readonly IJson Json = new NewtonsoftJson();
        protected static readonly ILoggerFactory Loggers = new NullLoggerFactory();

        private readonly TestReport _report;

        protected ConformanceSuite(TestReport report) => _report = report;

        /// <summary>The MCP feature this suite covers, shown as the section header in the run log.</summary>
        public abstract string Title { get; }

        /// <summary>Registers and runs every test in this suite via <see cref="Test"/>.</summary>
        public abstract Task Run();

        protected Task Test(string name, Func<Task> body) => _report.Test(name, body);
        protected void Assert(bool condition, string name) => _report.Assert(condition, name);
        protected void AssertEqual(string expected, string actual, string name) => _report.AssertEqual(expected, actual, name);

        // -- Servers & clients ---------------------------------------------------------------

        /// <summary>The standard tools server: a forecast tool plus a structured-output 'add' tool.</summary>
        protected IServer BuildServer(InMemoryTransport serverEnd)
        {
            return new ServerBuilder()
                .WithName("Conf Server")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithDefaultToolsCapability(Json, tools =>
                {
                    tools.AddTool(new TestToolHandler());
                    tools.AddTool(new StructuredToolHandler(Json));
                })
                .Build();
        }

        /// <summary>A plain client with no client-side capabilities, over the given loopback end.</summary>
        protected IClient ConnectClient(InMemoryTransport clientEnd)
        {
            return new ClientBuilder()
                .WithName("Conf Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();
        }

        /// <summary>A client that advertises whichever client-side capabilities are supplied.</summary>
        protected IClient ConnectClientWith(
            InMemoryTransport clientEnd,
            IElicitationCapabilityFactory elicitation = null,
            ISamplingCapabilityFactory sampling = null,
            IRootsCapabilityFactory roots = null)
        {
            var builder = new ClientBuilder()
                .WithName("Conf Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd));

            if (elicitation != null)
                builder.WithElicitationCapability(elicitation);
            if (sampling != null)
                builder.WithSamplingCapability(sampling);
            if (roots != null)
                builder.WithRootsCapability(roots);

            return builder.Build();
        }

        /// <summary>
        /// Builds a real <see cref="McpClient"/> ↔ server pair over the loopback transport and completes the
        /// initialize handshake, returning both raw transport ends so a test can also drive server→client
        /// traffic directly.
        /// </summary>
        protected async Task<(IClient client, InMemoryTransport clientEnd, InMemoryTransport serverEnd)> ConnectedPair()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            return (client, clientEnd, serverEnd);
        }

        /// <summary>
        /// Completes the initialize handshake over a raw loopback client end so the server's lifecycle gate
        /// lets subsequent requests through. For tests that drive a full <see cref="McpServer"/> with raw
        /// <c>SendRequest</c> calls rather than a real <see cref="McpClient"/> (which handshakes in Connect).
        /// </summary>
        protected Task Handshake(InMemoryTransport clientEnd)
        {
            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("Conf", "1.0.0"));
            return clientEnd.SendRequest("initialize", init.WriteMembers);
        }

        /// <summary>Wires a bare transport to answer "initialize" with a fixed protocol version.</summary>
        protected void ActAsRawServer(InMemoryTransport serverEnd, string versionToReturn)
        {
            serverEnd.RequestReceived += request =>
            {
                if (request.Method != "initialize")
                    return;

                var result = new InitializeResult(versionToReturn, new ServerCapabilitiesModel(), new ServerInfo("Raw Server", "1.0.0"));
                _ = serverEnd.SendResponse(JsonRpcResponse.Ok(request.Id, result.WriteMembers));
            };
        }

        // -- Wire inspection -----------------------------------------------------------------

        /// <summary>Finds the first message carrying an <c>initialize</c> result (has a protocolVersion).</summary>
        protected static IJsonObject FindInitializeResult(List<string> messages)
        {
            foreach (var message in Snapshot(messages))
            {
                var result = Json.Parse(message)["result"]?.AsObject();
                if (result?["protocolVersion"] != null)
                    return result;
            }
            return null;
        }

        /// <summary>Pulls the <c>capabilities</c> object out of the client's sent <c>initialize</c> request.</summary>
        protected static IJsonObject FindInitializeCapabilities(List<string> sent)
        {
            foreach (var message in Snapshot(sent))
            {
                var parsed = Json.Parse(message);
                if (parsed["method"]?.AsString() == "initialize")
                    return parsed["params"]?.AsObject()["capabilities"]?.AsObject();
            }
            return null;
        }

        /// <summary>Takes a thread-safe copy of a transport's Sent/Received log for assertion.</summary>
        protected static List<string> Snapshot(List<string> list)
        {
            lock (list)
                return new List<string>(list);
        }

        /// <summary>Polls <paramref name="condition"/> until it holds or the timeout elapses.</summary>
        protected static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000)
        {
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.ElapsedMilliseconds < timeoutMs)
            {
                if (condition())
                    return true;
                await Task.Delay(10);
            }
            return condition();
        }

        protected static async Task WithTimeout(Task task, int ms, string what)
        {
            if (await Task.WhenAny(task, Task.Delay(ms)) != task)
                throw new TimeoutException($"{what} timed out after {ms}ms");
            await task;
        }

        protected static async Task<T> WithTimeout<T>(Task<T> task, int ms, string what)
        {
            if (await Task.WhenAny(task, Task.Delay(ms)) != task)
                throw new TimeoutException($"{what} timed out after {ms}ms");
            return await task;
        }

        /// <summary>
        /// Resolves how to launch this same test assembly in <c>stdio-server</c> mode. Prefers the
        /// platform apphost next to the entry dll; falls back to the dotnet muxer.
        /// </summary>
        protected static (string command, string arguments) ResolveStdioServerCommand()
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
    }
}
