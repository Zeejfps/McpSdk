#nullable disable
using System;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Roots (the client-offered filesystem-boundary capability): a client with a roots controller
    /// advertises <c>roots</c> (with <c>listChanged</c>) in initialize, answers a server→client
    /// <c>roots/list</c> request with its roots, and — when it supports list-changed — emits
    /// <c>notifications/roots/list_changed</c> when its controller raises the event.
    /// </summary>
    public sealed class RootsTests : ConformanceSuite
    {
        public RootsTests(TestReport report) : base(report) { }

        public override string Title => "Roots";

        public override async Task Run()
        {
            await Test("client advertises the roots (listChanged) capability", RootsCapabilityDeclared);
            await Test("roots/list round-trips the client's roots", RootsListRoundTrips);
            await Test("notifications/roots/list_changed is emitted when roots change", RootsListChangedNotification);
        }

        private async Task RootsCapabilityDeclared()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestRootsController(listChangedSupported: true);
            var client = ConnectClientWith(clientEnd, roots: new TestRootsFactory(controller));
            await client.Connect();

            var roots = FindInitializeCapabilities(clientEnd.Sent)?["roots"]?.AsObject();
            Assert(roots != null, "client advertises the roots capability");
            Assert(roots?["listChanged"]?.AsBool() == true, "a list-changed-capable client declares roots.listChanged");
        }

        private async Task RootsListRoundTrips()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestRootsController(listChangedSupported: false)
            {
                RootsToReturn = new[]
                {
                    new Root("file:///home/project", "project"),
                    new Root("file:///tmp", "tmp"),
                },
            };
            var client = ConnectClientWith(clientEnd, roots: new TestRootsFactory(controller));
            await client.Connect();

            // The server side asks the client for its roots over the raw transport.
            var response = await serverEnd.SendRequest("roots/list", _ => { });
            Assert(response.IsOk, "client answers roots/list with a result");

            var result = new ListRootsResult(response.Result);
            Assert(result.Roots.Length == 2, "both roots round-trip");
            Assert(result.Roots.Any(r => r.Uri == "file:///home/project"), "the first root uri round-trips");
            Assert(result.Roots.Any(r => r.Name == "tmp"), "the second root name round-trips");
        }

        private async Task RootsListChangedNotification()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            ActAsRawServer(serverEnd, ProtocolVersion.Latest);
            await serverEnd.Start();

            var controller = new TestRootsController(listChangedSupported: true);
            var client = ConnectClientWith(clientEnd, roots: new TestRootsFactory(controller));
            await client.Connect();

            controller.RaiseListChanged();
            var got = await WaitUntil(() => Snapshot(clientEnd.Sent).Any(m =>
                Json.Parse(m)["method"]?.AsString() == "notifications/roots/list_changed"));
            Assert(got, "client emits notifications/roots/list_changed when its roots change");
        }

        private sealed class TestRootsController : IRootsController
        {
            private readonly bool _listChangedSupported;

            public TestRootsController(bool listChangedSupported)
            {
                _listChangedSupported = listChangedSupported;
            }

            public Root[] RootsToReturn { get; set; } = Array.Empty<Root>();

            public event Action ListChanged;
            public bool IsListChangedNotificationSupported => _listChangedSupported;

            public Task<ListRootsResult> ListRoots() => Task.FromResult(new ListRootsResult(RootsToReturn));

            public void RaiseListChanged() => ListChanged?.Invoke();
        }

        private sealed class TestRootsFactory : IRootsCapabilityFactory
        {
            private readonly IRootsController _controller;
            public TestRootsFactory(IRootsController controller) => _controller = controller;
            public IRootsController Create() => _controller;
        }
    }
}
