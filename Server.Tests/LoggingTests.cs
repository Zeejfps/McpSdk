#nullable disable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

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
            await Test("logging/setLevel with an unknown level returns InvalidParams", SetLevelUnknownLevelIsInvalidParams);
            await Test("logging/setLevel -> MethodNotFound when logging not enabled", LoggingNotConfiguredIsMethodNotFound);
        }

        private async Task LoggingRoundTripAndFiltering()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithLoggingCapability()
                .Build();
            await server.Start();

            var received = new List<LogMessage>();
            var client = new ClientBuilder()
                .WithName("Conf Client").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();
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

        private async Task SetLevelUnknownLevelIsInvalidParams()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithLoggingCapability()
                .Build();
            await server.Start();
            await clientEnd.Start();
            await Handshake(clientEnd);

            // A level the spec doesn't define must be rejected as InvalidParams (-32602), not swallowed or
            // collapsed into a generic InternalError.
            var resp = await clientEnd.SendRequest("logging/setLevel", w => w.Write("level", "bogus"));
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.InvalidParams,
                "logging/setLevel with an unknown level returns InvalidParams (-32602)");
        }

        private async Task LoggingNotConfiguredIsMethodNotFound()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd); // no logging capability
            await server.Start();
            await clientEnd.Start();
            await Handshake(clientEnd);

            var resp = await clientEnd.SendRequest("logging/setLevel", new SetLevelRequest(LoggingLevel.Debug).WriteMembers);
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.MethodNotFound,
                "logging/setLevel -> MethodNotFound when logging is not enabled");
        }
    }
}
