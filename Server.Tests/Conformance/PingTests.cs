#nullable disable
using System.Threading.Tasks;
using McpSdk.Protocol;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// The <c>ping</c> base-protocol utility in both directions, plus the unknown-method contract on the
    /// client: a server ping is answered with an empty result, a client ping is answered by the server,
    /// and an unknown server→client request returns MethodNotFound instead of hanging forever.
    /// </summary>
    public sealed class PingTests : ConformanceSuite
    {
        public PingTests(TestReport report) : base(report) { }

        public override string Title => "Ping & unknown-method handling";

        public override async Task Run()
        {
            await Test("server answers a client ping with an empty result", ServerAnswersClientPing);
            await Test("client answers a server->client ping", ClientAnswersServerPing);
            await Test("client returns MethodNotFound for an unknown request (no hang)", ClientRejectsUnknownRequest);
        }

        private async Task ServerAnswersClientPing()
        {
            var (client, _, _) = await ConnectedPair();

            var threw = false;
            try
            {
                await client.Ping();
            }
            catch
            {
                threw = true;
            }

            Assert(!threw, "client.Ping() completes (server replied with an empty result, not an error)");
        }

        private async Task ClientAnswersServerPing()
        {
            var (_, _, serverEnd) = await ConnectedPair();

            // The server side pings the client over the raw transport and awaits the reply.
            var response = await serverEnd.SendRequest("ping", _ => { });

            Assert(response.IsOk, "client answers a server->client ping with a (non-error) result");
        }

        private async Task ClientRejectsUnknownRequest()
        {
            var (_, _, serverEnd) = await ConnectedPair();

            // Before the fix this hung forever (the client silently dropped unknown requests).
            var response = await serverEnd.SendRequest("does/not/exist", _ => { });

            Assert(response.IsError, "client returns an error for an unknown server->client method (no hang)");
            Assert(response.Error?.Code == ErrorCode.MethodNotFound,
                "unknown server->client method returns MethodNotFound (-32601)");
        }
    }
}
