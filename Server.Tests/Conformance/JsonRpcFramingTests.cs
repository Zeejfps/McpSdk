#nullable disable
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// JSON-RPC base-protocol correctness: stdio framing collapses outgoing messages to a single
    /// newline-delimited line without mangling escaped content, top-level arrays are detected as
    /// (removed-in-2025-06-18) batches and rejected rather than processed, and a string request id is
    /// echoed back as a string rather than coerced to a number.
    /// </summary>
    public sealed class JsonRpcFramingTests : ConformanceSuite
    {
        public JsonRpcFramingTests(TestReport report) : base(report) { }

        public override string Title => "JSON-RPC framing & request ids";

        public override async Task Run()
        {
            await Test("framing collapses embedded CR/LF/TAB to a single line", FramingStripsEmbeddedControl);
            await Test("framing preserves escaped newlines inside string values", FramingPreservesEscapedNewlines);
            await Test("batch detection flags arrays, not objects", BatchDetection);
            await Test("server ignores an incoming JSON-RPC batch (no response)", BatchRejectionRoundTrip);
            await Test("string request id is echoed back as a string", StringRequestId);
        }

        private Task FramingStripsEmbeddedControl()
        {
            const string dirty = "{\"a\":\t1,\n\"b\":2}\r\n";
            var line = JsonRpcFraming.ToSingleLine(dirty);

            AssertEqual("{\"a\":1,\"b\":2}", line, "embedded CR/LF/TAB stripped");
            Assert(!line.Contains('\n') && !line.Contains('\r') && !line.Contains('\t'),
                "no raw control characters remain in the frame");
            return Task.CompletedTask;
        }

        private Task FramingPreservesEscapedNewlines()
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

        private Task BatchDetection()
        {
            Assert(JsonRpcFraming.IsBatch("[{\"jsonrpc\":\"2.0\"}]"), "a top-level array is a batch");
            Assert(JsonRpcFraming.IsBatch("  \t [ ]"), "leading whitespace before '[' is still a batch");
            Assert(!JsonRpcFraming.IsBatch("{\"jsonrpc\":\"2.0\"}"), "a top-level object is not a batch");
            Assert(!JsonRpcFraming.IsBatch("   {}"), "leading whitespace before '{' is not a batch");
            Assert(!JsonRpcFraming.IsBatch(""), "an empty message is not a batch");
            return Task.CompletedTask;
        }

        private async Task BatchRejectionRoundTrip()
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

        private async Task StringRequestId()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            const string id = "init-string-1";
            var request = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("Str", "1.0.0"));
            var raw = Json.Stringify(w =>
            {
                w.Write("jsonrpc", "2.0");
                w.Write("id", id);
                w.Write("method", "initialize");
                w.Write("params", request.WriteMembers);
            });
            await clientEnd.SendRaw(raw);

            var arrived = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m => IsResultForId(m, id)));
            Assert(arrived, "a response for the string id arrived");

            var responseMsg = Snapshot(clientEnd.Received).FirstOrDefault(m => IsResultForId(m, id));
            Assert(responseMsg != null, "response message located");
            if (responseMsg == null)
                return;

            var response = Json.Parse(responseMsg);
            Assert(response["id"].IsString, "echoed id is a string, not coerced to a number");
            AssertEqual(id, response["id"].AsString(), "echoed string id matches");
            AssertEqual(ProtocolVersion.Latest, response["result"].AsObject()["protocolVersion"]?.AsString(), "string-id initialize negotiated correctly");
        }

        private static bool IsResultForId(string message, string id)
        {
            var obj = Json.Parse(message);
            var idProp = obj["id"];
            return obj["result"] != null && idProp != null && idProp.IsString && idProp.AsString() == id;
        }
    }
}
