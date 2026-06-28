#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Client;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ClientCapabilities;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Phase H conformance: the base-protocol utilities and server utility methods that the
    /// changelog-driven phases A–G missed — ping, the client unknown-method fix, cancellation,
    /// progress, logging, completion wiring, and resource subscriptions.
    /// </summary>
    public static partial class ConformanceTests
    {
        // -- H.1 Ping + unknown-method correctness -------------------------------------------

        private static async Task ServerAnswersClientPing()
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

        private static async Task ClientAnswersServerPing()
        {
            var (_, clientEnd, serverEnd) = await ConnectedPair();

            // The server side pings the client over the raw transport and awaits the reply.
            var response = await serverEnd.SendRequest("ping", _ => { });

            Assert(response.IsOk, "client answers a server->client ping with a (non-error) result");
        }

        private static async Task ClientRejectsUnknownRequest()
        {
            var (_, clientEnd, serverEnd) = await ConnectedPair();

            // Before the fix this hung forever (the client silently dropped unknown requests).
            var response = await serverEnd.SendRequest("does/not/exist", _ => { });

            Assert(response.IsError, "client returns an error for an unknown server->client method (no hang)");
            Assert(response.Error?.Code == ErrorCode.MethodNotFound,
                "unknown server->client method returns MethodNotFound (-32601)");
        }

        // -- H.5 Completion wiring -----------------------------------------------------------

        private sealed class StubCompletionController : ICompletionController
        {
            public Task<CompletionResult> Complete(CompletionRequest request, McpRequestContext context)
            {
                var prefix = request.Arguments?["value"]?.AsString() ?? "";
                return Task.FromResult(new CompletionResult(new[] { prefix + "_one", prefix + "_two" }, total: 2));
            }
        }

        private static async Task CompletionCompleteRoundTrips()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithCompletionCapability(new StubCompletionController())
                .Build();
            await server.Start();
            await clientEnd.Start();

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            Assert(new InitializeResult(initResp.Result).Capabilities?.Completion != null,
                "server advertises the completion capability when a controller is configured");

            var req = new CompletionRequest(
                new PromptReference("code_review"),
                Json.Object(w => { w.Write("name", "language"); w.Write("value", "py"); }));
            var resp = await clientEnd.SendRequest("completion/complete", req.WriteMembers);

            Assert(resp.IsOk, "completion/complete returns a result");
            var completion = resp.Result?["completion"]?.AsObject();
            Assert(completion != null, "completion/complete nests suggestions under 'completion'");
            var values = completion?["values"]?.AsStringArray();
            Assert(values != null && values.Length == 2 && values[0] == "py_one", "completion values round-trip");
            Assert(completion?["total"]?.AsInt() == 2, "completion total round-trips");
        }

        private static async Task CompletionNotConfiguredIsMethodNotFound()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd); // tools only, no completion controller
            await server.Start();
            await clientEnd.Start();

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            Assert(new InitializeResult(initResp.Result).Capabilities?.Completion == null,
                "completion capability is absent when no controller is configured");

            var req = new CompletionRequest(new PromptReference("x"),
                Json.Object(w => { w.Write("name", "a"); w.Write("value", "b"); }));
            var resp = await clientEnd.SendRequest("completion/complete", req.WriteMembers);
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.MethodNotFound,
                "completion/complete -> MethodNotFound when not configured");
        }

        // -- H.4 Logging ---------------------------------------------------------------------

        private static async Task LoggingRoundTripAndFiltering()
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
                .WithName("Phase H Client").WithVersion("1.0.0")
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

        private static async Task LoggingNotConfiguredIsMethodNotFound()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd); // no logging capability
            await server.Start();
            await clientEnd.Start();

            var resp = await clientEnd.SendRequest("logging/setLevel", new SetLevelRequest(LoggingLevel.Debug).WriteMembers);
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.MethodNotFound,
                "logging/setLevel -> MethodNotFound when logging is not enabled");
        }

        // -- H.6 Resource subscriptions + notifications --------------------------------------

        private sealed class SubscribableResourcesController : IResourcesController
        {
            private readonly bool _subscribeSupported;

            public SubscribableResourcesController(bool subscribeSupported)
            {
                _subscribeSupported = subscribeSupported;
            }

            public event System.Action ListChanged;
            public event System.Action<string> ResourceUpdated;

            public bool? IsResourceChangedNotificationSupported => _subscribeSupported;
            public bool? IsListChangedNotificationSupported => true;

            public List<string> Subscribed { get; } = new();

            public Task<ListTemplatesResult> ListTemplates(ListTemplatesRequest request, McpRequestContext context)
                => Task.FromResult(new ListTemplatesResult(System.Array.Empty<ResourceTemplate>()));

            public Task<ListResourcesResult> ListResources(ListResourcesRequest request, McpRequestContext context)
                => Task.FromResult(new ListResourcesResult(System.Array.Empty<Resource>()));

            public Task<ReadResourceResult> ReadResource(ReadResourceRequest readResourceRequest, McpRequestContext context)
                => Task.FromResult<ReadResourceResult>(null);

            public Task Subscribe(string uri, McpRequestContext context) { lock (Subscribed) Subscribed.Add(uri); return Task.CompletedTask; }
            public Task Unsubscribe(string uri, McpRequestContext context) { lock (Subscribed) Subscribed.Remove(uri); return Task.CompletedTask; }

            public void RaiseUpdated(string uri) => ResourceUpdated?.Invoke(uri);
            public void RaiseListChanged() => ListChanged?.Invoke();

            public bool HasSubscription(string uri) { lock (Subscribed) return Subscribed.Contains(uri); }
        }

        private static async Task ResourceSubscribeAndNotifications()
        {
            var controller = new SubscribableResourcesController(subscribeSupported: true);
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithResourcesCapability(controller)
                .Build();
            await server.Start();
            await clientEnd.Start();

            var init = new InitializeRequest(ProtocolVersion.Latest, new ClientCapabilitiesModel(), new ClientInfo("C", "1.0.0"));
            var initResp = await clientEnd.SendRequest("initialize", init.WriteMembers);
            var resourcesCap = initResp.Result?["capabilities"]?.AsObject()?["resources"]?.AsObject();
            Assert(resourcesCap?["subscribe"]?.AsBool() == true, "server advertises resources.subscribe");

            var subResp = await clientEnd.SendRequest("resources/subscribe", w => w.Write("uri", "file://x"));
            Assert(subResp.IsOk, "resources/subscribe returns a result");
            Assert(controller.HasSubscription("file://x"), "controller recorded the subscription");

            controller.RaiseUpdated("file://x");
            var gotUpdated = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
            {
                var o = Json.Parse(m);
                return o["method"]?.AsString() == "notifications/resources/updated"
                    && o["params"]?.AsObject()?["uri"]?.AsString() == "file://x";
            }));
            Assert(gotUpdated, "notifications/resources/updated reaches the client carrying the uri");

            controller.RaiseListChanged();
            var gotListChanged = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
                Json.Parse(m)["method"]?.AsString() == "notifications/resources/list_changed"));
            Assert(gotListChanged, "notifications/resources/list_changed reaches the client");

            var unsubResp = await clientEnd.SendRequest("resources/unsubscribe", w => w.Write("uri", "file://x"));
            Assert(unsubResp.IsOk, "resources/unsubscribe returns a result");
            Assert(!controller.HasSubscription("file://x"), "controller removed the subscription");
        }

        private static async Task ResourceSubscribeNotAdvertisedIsMethodNotFound()
        {
            var controller = new SubscribableResourcesController(subscribeSupported: false);
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithResourcesCapability(controller)
                .Build();
            await server.Start();
            await clientEnd.Start();

            var resp = await clientEnd.SendRequest("resources/subscribe", w => w.Write("uri", "file://x"));
            Assert(resp.IsError && resp.Error?.Code == ErrorCode.MethodNotFound,
                "resources/subscribe -> MethodNotFound when subscribe is not advertised");
        }

        // -- H.2 Cancellation ----------------------------------------------------------------

        private sealed class CancellableToolHandler : IToolHandler
        {
            public Tool Tool { get; } = new Tool("slow", "blocks until cancelled", new ObjectSchema());
            public volatile bool Started;
            public volatile bool Cancelled;

            public async Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
            {
                var token = context?.CancellationToken ?? CancellationToken.None;
                Started = true;
                try
                {
                    await Task.Delay(Timeout.Infinite, token);
                }
                catch (OperationCanceledException)
                {
                    Cancelled = true;
                    throw;
                }

                return new CallToolResult(new Content[] { new TextContent("done") }, false);
            }
        }

        private static async Task ClientCancellationStopsServerWork()
        {
            var tool = new CancellableToolHandler();
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithDefaultToolsCapability(Json, tools => tools.AddTool(tool))
                .Build();
            await server.Start();

            var client = new ClientBuilder()
                .WithName("Phase H Client").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();
            await client.Connect();

            var cts = new CancellationTokenSource();
            var callTask = client.CallTool(new CallToolRequest("slow", Json.Object(_ => { })), cts.Token);

            var started = await WaitUntil(() => tool.Started);
            Assert(started, "server tool started running");
            cts.Cancel();

            var threw = false;
            try { await callTask; }
            catch (OperationCanceledException) { threw = true; }
            Assert(threw, "client CallTool throws OperationCanceledException when cancelled");

            var observed = await WaitUntil(() => tool.Cancelled);
            Assert(observed, "server tool observed cancellation via McpRequestContext (from notifications/cancelled)");
        }

        // -- H.3 Progress --------------------------------------------------------------------

        private sealed class ProgressToolHandler : IToolHandler
        {
            public Tool Tool { get; } = new Tool("progress-tool", "reports progress", new ObjectSchema());

            public async Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
            {
                if (context != null)
                    await context.Progress.Report(0.5, 1.0, "halfway");
                return new CallToolResult(new Content[] { new TextContent("done") }, false);
            }
        }

        private static IServer BuildProgressServer(InMemoryTransport serverEnd) =>
            new ServerBuilder()
                .WithName("Conf Server").WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithDefaultToolsCapability(Json, tools => tools.AddTool(new ProgressToolHandler()))
                .Build();

        private static async Task ProgressEmittedWhenTokenPresent()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildProgressServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            await clientEnd.SendRequest("tools/call", w =>
            {
                w.Write("name", "progress-tool");
                w.Write("arguments", Json.Object(_ => { }));
                w.Write("_meta", Json.Object(m => m.Write("progressToken", "p1")));
            });

            var got = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m =>
            {
                var o = Json.Parse(m);
                return o["method"]?.AsString() == "notifications/progress"
                    && o["params"]?.AsObject()?["progressToken"]?.AsString() == "p1";
            }));
            Assert(got, "server emits notifications/progress keyed to the request's progressToken");
        }

        private static async Task ProgressNotEmittedWithoutToken()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildProgressServer(serverEnd);
            await server.Start();
            await clientEnd.Start();

            await clientEnd.SendRequest("tools/call", w =>
            {
                w.Write("name", "progress-tool");
                w.Write("arguments", Json.Object(_ => { }));
            });

            var completed = await WaitUntil(() => Snapshot(clientEnd.Received).Any(m => Json.Parse(m)["result"] != null));
            Assert(completed, "tools/call completed");
            Assert(!Snapshot(clientEnd.Received).Any(m => Json.Parse(m)["method"]?.AsString() == "notifications/progress"),
                "no progress notification is emitted when the request carries no progressToken");
        }

        private static async Task ClientDispatchesProgress()
        {
            var (client, _, serverEnd) = await ConnectedPair();

            ProgressNotification received = null;
            client.ProgressReceived += p => received = p;

            await serverEnd.SendNotification("notifications/progress",
                new ProgressNotification(new RequestId("p9"), 0.42, 1.0, "working").WriteMembers);

            var got = await WaitUntil(() => received != null);
            Assert(got, "client raises ProgressReceived for an inbound notifications/progress");
            Assert(received?.Progress == 0.42 && received.ProgressToken.StringValue == "p9",
                "progress fields round-trip to the client handler");
        }

        // -- Helpers -------------------------------------------------------------------------

        /// <summary>
        /// Builds a real <see cref="McpClient"/> ↔ <see cref="McpServer"/> pair over the loopback
        /// transport and completes the initialize handshake, returning both raw transport ends so a
        /// test can also drive server→client traffic directly.
        /// </summary>
        private static async Task<(IClient client, InMemoryTransport clientEnd, InMemoryTransport serverEnd)> ConnectedPair()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = new ClientBuilder()
                .WithName("Phase H Client")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(clientEnd))
                .Build();
            await client.Connect();

            return (client, clientEnd, serverEnd);
        }
    }
}
