#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Protocol-version negotiation across the initialize handshake, in every direction: a modern peer
    /// pair, a legacy client against a modern server, an unsupported client version (server never errors),
    /// and a modern client against legacy / unsupported servers. Also confirms the <c>InitializeResult</c>
    /// parses capabilities + serverInfo. Driven over the in-process loopback transport.
    /// </summary>
    public sealed class ProtocolNegotiationTests : ConformanceSuite
    {
        public ProtocolNegotiationTests(TestReport report) : base(report) { }

        public override string Title => "Protocol version negotiation";

        public override async Task Run()
        {
            await Test("modern peer full handshake (real client <-> real server)", ModernPeerHandshake);
            await Test("legacy client (2024-11-05) -> modern server echoes legacy version", LegacyClientNegotiation);
            await Test("unsupported client version -> server offers Latest, never errors", UnsupportedClientNegotiation);
            await Test("modern client -> legacy server (2024-11-05) connects", ModernClientLegacyServer);
            await Test("modern client -> unsupported server disconnects cleanly", ModernClientUnsupportedServer);
            await Test("InitializeResult parses capabilities + serverInfo", InitializeResultParsing);
        }

        private async Task ModernPeerHandshake()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            Assert(client.IsConnected, "client reports connected");

            var initResult = FindInitializeResult(serverEnd.Sent);
            Assert(initResult != null, "server emitted an initialize result");
            AssertEqual(ProtocolVersion.Latest, initResult?["protocolVersion"]?.AsString(), "negotiated version is Latest");

            var tools = await client.ListTools();
            Assert(tools.Tools.Any(t => t.Name == "get-forecast"), "tools/list round-trips after handshake");
        }

        private async Task LegacyClientNegotiation()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            var request = new InitializeRequest("2024-11-05", new ClientCapabilitiesModel(), new ClientInfo("Legacy", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", request.WriteMembers);

            Assert(response.IsOk, "server responded OK to legacy initialize");
            AssertEqual("2024-11-05", response.Result?["protocolVersion"]?.AsString(), "server echoes the legacy version");
        }

        private async Task UnsupportedClientNegotiation()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            var request = new InitializeRequest("1999-01-01", new ClientCapabilitiesModel(), new ClientInfo("Weird", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", request.WriteMembers);

            Assert(response.IsOk, "server did not error on an unsupported version");
            AssertEqual(ProtocolVersion.Latest, response.Result?["protocolVersion"]?.AsString(), "server offers Latest to an unsupported client");
        }

        private async Task ModernClientLegacyServer()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, "2024-11-05");
            await serverEnd.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            Assert(client.IsConnected, "client connects to a legacy server");

            var notifiedInitialized = Snapshot(clientEnd.Sent)
                .Any(m => Json.Parse(m)["method"]?.AsString() == "notifications/initialized");
            Assert(notifiedInitialized, "client sends 'notifications/initialized' (not 'initialized')");
        }

        private async Task ModernClientUnsupportedServer()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, "1999-01-01");
            await serverEnd.Start();

            var client = ConnectClient(clientEnd);

            var threw = false;
            try
            {
                await client.Connect();
            }
            catch (ClientException)
            {
                threw = true;
            }

            Assert(threw, "client throws ClientException on an unsupported server version");
            Assert(!client.IsConnected, "client is not connected after a failed negotiation");
        }

        private async Task InitializeResultParsing()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            var request = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("Parse", "1.0.0"));
            var response = await clientEnd.SendRequest("initialize", request.WriteMembers);
            var parsed = new InitializeResult(response.Result);

            Assert(parsed.Capabilities != null, "capabilities are parsed (not dropped)");
            Assert(parsed.Capabilities?.Tools != null, "tools capability is parsed");
            Assert(parsed.ServerInfo != null, "serverInfo is parsed");
            AssertEqual("Conf Server", parsed.ServerInfo?.Name, "serverInfo.name parsed from lowercase 'name'");
        }
    }
}
