#nullable disable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Server logging (<c>notifications/message</c> + <c>logging/setLevel</c>): a server that advertises
    /// the logging capability delivers logs at or above the level the client set and silently drops those
    /// below it, and <c>logging/setLevel</c> returns MethodNotFound when logging was never enabled.
    /// </summary>
    public sealed class LoggingTests : ConformanceSuite
    {
        public LoggingTests(TestReport report) : base(report) { }

        public override string Title => "Logging";

        public override async Task Run()
        {
            await Test("logging: notifications/message round-trips + setLevel filters by severity", LoggingRoundTripAndFiltering);
            await Test("logging/setLevel -> MethodNotFound when logging not enabled", LoggingNotConfiguredIsMethodNotFound);
        }

        private async Task LoggingRoundTripAndFiltering()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var builder = new ServerBuilder("Conf Server", "1.0.0");
            builder.Context.AddNewtonsoftJson();
            builder.Context.AddInMemoryServerTransport(serverEnd);
            builder.Context.AddLoggingCapability();
            var server = builder.Build();
            await server.Start();

            var received = new List<LogMessage>();
            var client = ConnectClient(clientEnd);
            client.LogMessageReceived += m => { lock (received) received.Add(m); };
            await client.Connect();

            var initResult = FindInitializeResult(serverEnd.Sent);
            Assert(initResult?["capabilities"]?.AsObject()?["logging"] != null, "server advertises the logging capability");

            await client.SetLoggingLevel(LoggingLevel.Warning);

            await server.Log(LoggingLevel.Info, w => w.Write("msg", "below"));  // below the set level -> dropped
            await server.Log(LoggingLevel.Error, w => w.Write("msg", "above")); // at/above -> delivered

            var got = await WaitUntil(() =>
            {
                lock (received) return received.Any(m => m.Data?["msg"]?.AsString() == "above");
            });
            Assert(got, "an error log at/above the set level reaches the client");

            lock (received)
            {
                Assert(received.All(m => m.Data?["msg"]?.AsString() != "below"),
                    "an info log below the set level is filtered out (never sent)");
                var err = received.FirstOrDefault(m => m.Data?["msg"]?.AsString() == "above");
                Assert(err?.Level == LoggingLevel.Error, "the delivered log carries its severity level");
            }
        }

        private async Task LoggingNotConfiguredIsMethodNotFound()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd); // no logging capability
            await server.Start();
            await clientEnd.Start();

            // The advertisement is gated on AddLoggingCapability(): a server that never called it must not
            // advertise the logging capability at all (complements the positive case above).
            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            Assert(new InitializeResult(initResp.Result).Capabilities?.Logging == null,
                "logging capability is absent when AddLoggingCapability was not called");

            var resp = await clientEnd.SendRequest("logging/setLevel", new SetLevelRequest(LoggingLevel.Debug).WriteMembers);
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.MethodNotFound,
                "logging/setLevel -> MethodNotFound when logging is not enabled");
        }
    }
}
