#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.System.Text.Json;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Resolution checks for the shared serializer and logger <see cref="IContext"/> registrations
    /// (<c>AddNewtonsoftJson</c> / <c>AddSystemTextJson</c> / <c>AddLogger</c> / <c>AddConsoleLogger</c>).
    /// These have no natural feature home, so they ride in a dedicated suite. The two
    /// <c>AddConsoleLogger</c> overloads are namespace-scoped (server vs client), so they are invoked here
    /// via their fully-qualified static names to disambiguate the two extension methods of the same name.
    /// </summary>
    public sealed class RegistrationTests : ConformanceSuite
    {
        public RegistrationTests(TestReport report) : base(report) { }

        public override string Title => "Serializer & Logger Registrations";

        public override async Task Run()
        {
            await Test("AddNewtonsoftJson registers NewtonsoftJson as IJson", AddNewtonsoftJsonResolvesNewtonsoft);
            await Test("AddSystemTextJson registers SystemJson as IJson", AddSystemTextJsonResolvesSystemJson);
            await Test("AddLogger registers the supplied ILoggerFactory", AddLoggerResolvesSuppliedFactory);
            await Test("server AddConsoleLogger registers ServerConsoleLoggerFactory", ServerAddConsoleLoggerResolvesServerFactory);
            await Test("client AddConsoleLogger registers ClientConsoleLoggerFactory", ClientAddConsoleLoggerResolvesClientFactory);
            await Test("ServerBuilder ctor seeds Context with ServerInfoOptions and NullLoggerFactory", ServerBuilderCtorSeedsContext);
            await Test("ClientBuilder ctor seeds Context with ClientInfoOptions and NullLoggerFactory", ClientBuilderCtorSeedsContext);
            await Test("server ConfigureInfo flows Title/Description into resolved ServerInfo", ServerConfigureInfoFlowsTitleAndDescription);
            await Test("client ConfigureInfo flows Title/Description into resolved ClientInfo", ClientConfigureInfoFlowsTitleAndDescription);
            await Test("ServerInfo/ClientInfo resolve from ctor name/version without ConfigureInfo", InfoResolvesWithoutConfigureInfo);
            await Test("AddServerSession resolves a non-null McpServer with only transport+serializer (controllers null)", AddServerSessionResolvesMcpServer);
            await Test("new-API stdio server builds: AddStdioTransport + serializer -> non-null IServerHost", NewApiStdioServerBuilds);
            await Test("new-API stdio Build() throws on missing serializer (eager ITransport realization)", NewApiStdioMissingSerializerThrowsAtBuild);
            await Test("new-API Build() throws a clear 'no transport registered' error", NewApiNoTransportThrowsAtBuild);
            await Test("tools capability AGGREGATES: two AddToolsCapability calls merge via the composite", ToolsCapabilitiesAggregate);
            await Test("AddToolsCapability(IToolsController) leaf aggregates with a builder leaf", ToolsControllerOverloadAggregates);
            await Test("AddTool<THandler>() ACTIVATES the handler with its ctor dependency injected", AddToolGenericActivatesWithDependency);
            await Test("ZERO tools: no AddToolsCapability -> IToolsController is null (capability suppressed)", ZeroToolsCapabilitySuppressesController);
            await Test("WithPageSize(n) flows into the composite's tools/list paging", WithPageSizeFlowsIntoComposite);
            await Test("client AddStdioTransport registers a non-null ITransport without spawning a process", ClientAddStdioTransportResolvesTransport);
            await Test("client AddStreamableHttpTransport(string url) registers a non-null ITransport without opening a connection", ClientAddStreamableHttpTransportResolvesTransport);
            await Test("new-API client Build() throws a clear 'no transport registered' error", NewApiClientNoTransportThrowsAtBuild);
            await Test("new-API client builds: transport + AddRootsCapability(controller) -> non-null IClient", NewApiClientBuildsWithRootsCapability);
        }

        private Task AddNewtonsoftJsonResolvesNewtonsoft()
        {
            var container = new DiContainer();
            container.AddNewtonsoftJson();
            var provider = container.BuildServiceProvider();

            Assert(provider.GetService<IJson>() is NewtonsoftJson,
                "AddNewtonsoftJson resolves IJson as a NewtonsoftJson");
            return Task.CompletedTask;
        }

        private Task AddSystemTextJsonResolvesSystemJson()
        {
            var container = new DiContainer();
            container.AddSystemTextJson();
            var provider = container.BuildServiceProvider();

            Assert(provider.GetService<IJson>() is SystemJson,
                "AddSystemTextJson resolves IJson as a SystemJson");
            return Task.CompletedTask;
        }

        private Task AddLoggerResolvesSuppliedFactory()
        {
            var factory = new NullLoggerFactory();
            var container = new DiContainer();
            container.AddLogger(factory);
            var provider = container.BuildServiceProvider();

            Assert(ReferenceEquals(provider.GetService<ILoggerFactory>(), factory),
                "AddLogger resolves ILoggerFactory as the exact supplied instance");
            return Task.CompletedTask;
        }

        private Task ServerAddConsoleLoggerResolvesServerFactory()
        {
            var container = new DiContainer();
            McpSdk.Server.ConsoleLoggerContextExtensions.AddConsoleLogger(container);
            var provider = container.BuildServiceProvider();

            Assert(provider.GetService<ILoggerFactory>() is ServerConsoleLoggerFactory,
                "server AddConsoleLogger resolves ILoggerFactory as a ServerConsoleLoggerFactory");
            return Task.CompletedTask;
        }

        private Task ClientAddConsoleLoggerResolvesClientFactory()
        {
            var container = new DiContainer();
            McpSdk.Client.ConsoleLoggerContextExtensions.AddConsoleLogger(container);
            var provider = container.BuildServiceProvider();

            Assert(provider.GetService<ILoggerFactory>() is ClientConsoleLoggerFactory,
                "client AddConsoleLogger resolves ILoggerFactory as a ClientConsoleLoggerFactory");
            return Task.CompletedTask;
        }

        private Task ServerBuilderCtorSeedsContext()
        {
            var builder = new McpSdk.Server.ServerBuilder("n", "1.0");
            Assert(builder.Context != null, "ServerBuilder ctor exposes a non-null Context");

            var provider = ((DiContainer)builder.Context).BuildServiceProvider();
            var options = provider.GetService<McpSdk.Server.ServerInfoOptions>();
            Assert(options != null, "ServerBuilder Context resolves the seeded ServerInfoOptions");
            AssertEqual("n", options?.Name, "seeded ServerInfoOptions.Name");
            AssertEqual("1.0", options?.Version, "seeded ServerInfoOptions.Version");
            Assert(provider.GetService<ILoggerFactory>() is NullLoggerFactory,
                "ServerBuilder Context resolves the default ILoggerFactory as a NullLoggerFactory");
            return Task.CompletedTask;
        }

        private Task ClientBuilderCtorSeedsContext()
        {
            var builder = new McpSdk.Client.ClientBuilder("n", "1.0");
            Assert(builder.Context != null, "ClientBuilder ctor exposes a non-null Context");

            var provider = ((DiContainer)builder.Context).BuildServiceProvider();
            var options = provider.GetService<McpSdk.Client.ClientInfoOptions>();
            Assert(options != null, "ClientBuilder Context resolves the seeded ClientInfoOptions");
            AssertEqual("n", options?.Name, "seeded ClientInfoOptions.Name");
            AssertEqual("1.0", options?.Version, "seeded ClientInfoOptions.Version");
            Assert(provider.GetService<ILoggerFactory>() is NullLoggerFactory,
                "ClientBuilder Context resolves the default ILoggerFactory as a NullLoggerFactory");
            return Task.CompletedTask;
        }

        private Task ServerConfigureInfoFlowsTitleAndDescription()
        {
            var builder = new McpSdk.Server.ServerBuilder("MyServer", "2.0");
            // ConfigureInfo is called fully-qualified: both the server and client overloads extend IContext
            // with a lambda-compatible signature, so the unqualified form would be ambiguous in this project
            // (which references both). Real server code does `using McpSdk.Server;` and calls it unqualified.
            McpSdk.Server.ServerContextExtensions.ConfigureInfo(
                builder.Context, info => { info.Title = "T"; info.Description = "D"; });

            var provider = ((DiContainer)builder.Context).BuildServiceProvider();

            var options = provider.GetService<McpSdk.Server.ServerInfoOptions>();
            AssertEqual("T", options?.Title, "ConfigureInfo mutates ServerInfoOptions.Title");
            AssertEqual("D", options?.Description, "ConfigureInfo mutates ServerInfoOptions.Description");

            var info = provider.GetService<ServerInfo>();
            Assert(info != null, "ServerInfo resolves from the Context after ConfigureInfo");
            AssertEqual("MyServer", info?.Name, "resolved ServerInfo.Name comes from the ctor");
            AssertEqual("2.0", info?.Version, "resolved ServerInfo.Version comes from the ctor");
            AssertEqual("T", info?.Title, "resolved ServerInfo.Title flows from ConfigureInfo");
            AssertEqual("D", info?.Description, "resolved ServerInfo.Description flows from ConfigureInfo");
            return Task.CompletedTask;
        }

        private Task ClientConfigureInfoFlowsTitleAndDescription()
        {
            var builder = new McpSdk.Client.ClientBuilder("MyClient", "3.0");
            McpSdk.Client.ClientContextExtensions.ConfigureInfo(
                builder.Context, info => { info.Title = "CT"; info.Description = "CD"; });

            var provider = ((DiContainer)builder.Context).BuildServiceProvider();

            var options = provider.GetService<McpSdk.Client.ClientInfoOptions>();
            AssertEqual("CT", options?.Title, "ConfigureInfo mutates ClientInfoOptions.Title");
            AssertEqual("CD", options?.Description, "ConfigureInfo mutates ClientInfoOptions.Description");

            var info = provider.GetService<ClientInfo>();
            Assert(info != null, "ClientInfo resolves from the Context after ConfigureInfo");
            AssertEqual("MyClient", info?.Name, "resolved ClientInfo.Name comes from the ctor");
            AssertEqual("3.0", info?.Version, "resolved ClientInfo.Version comes from the ctor");
            AssertEqual("CT", info?.Title, "resolved ClientInfo.Title flows from ConfigureInfo");
            AssertEqual("CD", info?.Description, "resolved ClientInfo.Description flows from ConfigureInfo");
            return Task.CompletedTask;
        }

        private Task AddServerSessionResolvesMcpServer()
        {
            // T9: the session server (McpServer) is registered via a factory delegate, NOT type-injected —
            // its ctor's optional controllers + bool can't be satisfied by the reflection container. Start
            // from a builder Context (it seeds ServerInfo + NullLoggerFactory), add a serializer and a
            // transport instance, then register the session server via the factory helper. None of the four
            // controllers are registered: the factory pulls each with null-tolerant GetService<T>(), so the
            // resolve must succeed (null capabilities) rather than throw a type-injection error.
            var context = new McpSdk.Server.ServerBuilder("n", "v").Context;
            context.AddNewtonsoftJson();
            var (_, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            context.AddSingleton<ITransport>(serverEnd);
            McpSdk.Server.ServerSessionFactory.AddServerSession(context);

            var provider = ((DiContainer)context).BuildServiceProvider();

            // McpServer is internal to McpSdk.Server with no InternalsVisibleTo to this test assembly, so
            // resolve it by its runtime type via the non-generic GetService(Type) and assert identity by name.
            var mcpServerType = typeof(McpSdk.Server.ServerBuilder).Assembly.GetType("McpSdk.Server.McpServer");
            Assert(mcpServerType != null, "the McpSdk.Server assembly exposes the McpServer type");

            var server = provider.GetService(mcpServerType);
            Assert(server != null, "AddServerSession resolves a non-null session server (null controllers, no type-injection throw)");
            AssertEqual("McpServer", server?.GetType().Name, "the resolved session server is an McpServer");
            return Task.CompletedTask;
        }

        private Task NewApiStdioServerBuilds()
        {
            // T10: the new API builds a single-session stdio server purely from registrations — a serializer
            // plus AddStdioTransport() (which registers the ITransport, the IServerHost, and the McpServer
            // session). Build() resolves the single IServerHost and returns it as the IServer. Build() does
            // no I/O (the transport opens std handles only at Start()), so this is safe in-process.
            var builder = new McpSdk.Server.ServerBuilder("n", "1.0");
            builder.Context.AddNewtonsoftJson();
            builder.Context.AddStdioTransport();

            var server = builder.Build();
            Assert(server != null, "new-API stdio Build() returns a non-null IServer");
            Assert(server is McpSdk.Server.IServerHost, "the built stdio server is an IServerHost");
            return Task.CompletedTask;
        }

        private Task NewApiStdioMissingSerializerThrowsAtBuild()
        {
            // The stdio ITransport is registered as a singleton built with GetRequiredService<IJson>(), so it
            // is eagerly realized inside Build() and a missing serializer throws there (not later at Start()).
            var builder = new McpSdk.Server.ServerBuilder("n", "1.0");
            builder.Context.AddStdioTransport(); // no AddNewtonsoftJson() -> no IJson registered

            var threw = false;
            try { builder.Build(); }
            catch (Exception) { threw = true; }
            Assert(threw, "Build() throws when a transport is registered but no serializer (IJson) is");
            return Task.CompletedTask;
        }

        private Task NewApiNoTransportThrowsAtBuild()
        {
            // A serializer but no transport/host: Build()'s IServerHost probe is null, and because the builder
            // was created via the new (name, version) ctor it must throw a clear, actionable error rather than
            // NRE down the legacy WithX path.
            var builder = new McpSdk.Server.ServerBuilder("n", "1.0");
            builder.Context.AddNewtonsoftJson(); // serializer present, but no transport/host registered

            var threw = false;
            string message = null;
            try { builder.Build(); }
            catch (InvalidOperationException ex) { threw = true; message = ex.Message; }
            Assert(threw, "Build() throws an InvalidOperationException when no transport/host is registered");
            Assert(message != null && message.Contains("No transport registered"),
                "the no-transport Build() error names the missing transport registration");
            return Task.CompletedTask;
        }

        private Task InfoResolvesWithoutConfigureInfo()
        {
            var serverProvider = ((DiContainer)new McpSdk.Server.ServerBuilder("BareServer", "1.0").Context).BuildServiceProvider();
            var serverInfo = serverProvider.GetService<ServerInfo>();
            Assert(serverInfo != null, "ServerInfo resolves even when ConfigureInfo is never called");
            AssertEqual("BareServer", serverInfo?.Name, "default ServerInfo.Name comes from the ctor");
            AssertEqual("1.0", serverInfo?.Version, "default ServerInfo.Version comes from the ctor");
            Assert(serverInfo?.Title == null, "default ServerInfo.Title is null without ConfigureInfo");
            Assert(serverInfo?.Description == null, "default ServerInfo.Description is null without ConfigureInfo");

            var clientProvider = ((DiContainer)new McpSdk.Client.ClientBuilder("BareClient", "1.0").Context).BuildServiceProvider();
            var clientInfo = clientProvider.GetService<ClientInfo>();
            Assert(clientInfo != null, "ClientInfo resolves even when ConfigureInfo is never called");
            AssertEqual("BareClient", clientInfo?.Name, "default ClientInfo.Name comes from the ctor");
            AssertEqual("1.0", clientInfo?.Version, "default ClientInfo.Version comes from the ctor");
            Assert(clientInfo?.Title == null, "default ClientInfo.Title is null without ConfigureInfo");
            Assert(clientInfo?.Description == null, "default ClientInfo.Description is null without ConfigureInfo");
            return Task.CompletedTask;
        }

        private async Task ToolsCapabilitiesAggregate()
        {
            // T11/decision #2: per-session tools AGGREGATE, they don't replace. Two separate
            // AddToolsCapability(...) calls each register a leaf under the internal source marker; the public
            // IToolsController resolves to a CompositeToolsController that merges every leaf via GetServices.
            var context = new DiContainer();
            context.AddNewtonsoftJson();
            McpSdk.Server.ServerContextExtensions.AddToolsCapability(context, tools => tools.AddTool(new NamedToolHandler("tool-a")));
            McpSdk.Server.ServerContextExtensions.AddToolsCapability(context, tools => tools.AddTool(new NamedToolHandler("tool-b")));

            var provider = context.BuildServiceProvider();
            var controller = provider.GetService<IToolsController>();
            Assert(controller != null, "two AddToolsCapability calls resolve a non-null composite IToolsController");

            var names = await ListToolNames(controller);
            Assert(names.Contains("tool-a"), "the composite lists tool-a from the first leaf");
            Assert(names.Contains("tool-b"), "the composite lists tool-b from the second leaf");
        }

        private async Task ToolsControllerOverloadAggregates()
        {
            // The AddToolsCapability(IToolsController) overload registers a supplied controller as a leaf, and
            // it must aggregate with a builder leaf exactly like another builder call would.
            var context = new DiContainer();
            context.AddNewtonsoftJson();
            McpSdk.Server.ServerContextExtensions.AddToolsCapability(context, tools => tools.AddTool(new NamedToolHandler("builder-tool")));
            McpSdk.Server.ServerContextExtensions.AddToolsCapability(context, new SingleToolController("controller-tool"));

            var provider = context.BuildServiceProvider();
            var controller = provider.GetService<IToolsController>();

            var names = await ListToolNames(controller);
            Assert(names.Contains("builder-tool"), "the composite lists the builder leaf's tool");
            Assert(names.Contains("controller-tool"), "the composite lists the supplied-controller leaf's tool");
        }

        private async Task AddToolGenericActivatesWithDependency()
        {
            // T11/decision #7: AddTool<THandler>() is container-constructed via ActivatorUtilities at the scope
            // the leaf is resolved in. Registering a dependency and having the handler's tool name embed that
            // dependency's value proves the handler was activated with its ctor dependency injected.
            var context = new DiContainer();
            context.AddNewtonsoftJson();
            context.AddSingleton(new ToolDependency("injected"));
            McpSdk.Server.ServerContextExtensions.AddToolsCapability(context, tools => tools.AddTool<DependentToolHandler>());

            var provider = context.BuildServiceProvider();
            var controller = provider.GetService<IToolsController>();

            var names = await ListToolNames(controller);
            Assert(names.Contains("dep-tool-injected"),
                "AddTool<THandler>() activated the handler with its ToolDependency injected (tool 'dep-tool-injected' listed)");
        }

        private Task ZeroToolsCapabilitySuppressesController()
        {
            // SUPPRESSION: with no AddToolsCapability call there is no leaf and no composite, so the public
            // IToolsController resolves to null — the McpServer factory's null-tolerant probe then leaves the
            // tools capability unadvertised, preserving today's null-controller behavior.
            var context = new DiContainer();
            context.AddNewtonsoftJson();
            var provider = context.BuildServiceProvider();

            Assert(provider.GetService<IToolsController>() == null,
                "a context with no AddToolsCapability resolves a null IToolsController (tools capability suppressed)");
            return Task.CompletedTask;
        }

        private async Task WithPageSizeFlowsIntoComposite()
        {
            var context = new DiContainer();
            context.AddNewtonsoftJson();
            McpSdk.Server.ServerContextExtensions.AddToolsCapability(context, tools =>
            {
                tools.WithPageSize(1);
                tools.AddTool(new NamedToolHandler("page-a"));
                tools.AddTool(new NamedToolHandler("page-b"));
            });

            var provider = context.BuildServiceProvider();
            var controller = provider.GetService<IToolsController>();

            var firstPage = await controller.ListTools(new ListToolsRequest(), null);
            Assert(firstPage.Tools.Length == 1, "WithPageSize(1) makes the composite return one tool per page");
            Assert(firstPage.NextCursor != null, "a full first page advertises a nextCursor for the remaining tool");

            var secondPage = await controller.ListTools(new ListToolsRequest(firstPage.NextCursor), null);
            Assert(secondPage.Tools.Length == 1, "the composite walks to the second page via nextCursor");
            Assert(secondPage.NextCursor == null, "the final page carries no nextCursor");
        }

        private Task ClientAddStdioTransportResolvesTransport()
        {
            // T14: the client stdio transport is a registration (transport registered directly, decision #4).
            // The factory resolves IJson from DI and constructs the transport; construction is I/O-free — the
            // child process is spawned only in OnStart (at Connect()/Start()) — so eagerly realizing this
            // singleton inside BuildServiceProvider() does NOT spawn 'dummy-cmd'. Fully qualified to pick the
            // McpSdk.Client overload (the server side has a no-arg AddStdioTransport of the same name).
            var container = new DiContainer();
            container.AddNewtonsoftJson();
            // The real ClientBuilder ctor seeds a NullLoggerFactory; the transport's base ctor needs one, so a
            // bare DiContainer must register it (the extension pulls ILoggerFactory null-tolerantly, like server).
            container.AddLogger(new NullLoggerFactory());
            McpSdk.Client.StdioClientTransportExtensions.AddStdioTransport(container, "dummy-cmd", "arg");

            var provider = container.BuildServiceProvider();

            Assert(provider.GetService<ITransport>() != null,
                "client AddStdioTransport resolves a non-null ITransport (constructed from DI IJson, process not spawned)");
            return Task.CompletedTask;
        }

        private Task ClientAddStreamableHttpTransportResolvesTransport()
        {
            // T14: the client Streamable HTTP transport takes a bare endpointUrl STRING (not a pre-built
            // adapter) and builds the StreamableHttpClientAdapter internally from the url + DI ILoggerFactory.
            // Constructing the adapter only creates an HttpClient — no connection is opened — so eagerly
            // realizing this singleton inside BuildServiceProvider() is I/O-free even with an unreachable url.
            var container = new DiContainer();
            container.AddNewtonsoftJson();
            // The real ClientBuilder ctor seeds a NullLoggerFactory; the adapter + transport need one.
            container.AddLogger(new NullLoggerFactory());
            McpSdk.Adapter.StreamableHttpClient.StreamableHttpClientTransportExtensions.AddStreamableHttpTransport(
                container, "http://localhost:9/mcp");

            var provider = container.BuildServiceProvider();

            Assert(provider.GetService<ITransport>() != null,
                "client AddStreamableHttpTransport(string url) resolves a non-null ITransport (adapter built internally, no connection opened)");
            return Task.CompletedTask;
        }

        private Task NewApiClientNoTransportThrowsAtBuild()
        {
            // T15: parity with the server. A client built via the new (name, version) ctor with no ITransport
            // registered has nothing to connect over, so Build() must throw a clear, actionable error rather
            // than NRE down the legacy WithX path (matching today's ClientBuilder throw-on-null-transport).
            var builder = new McpSdk.Client.ClientBuilder("n", "1.0"); // no transport registered

            var threw = false;
            string message = null;
            try { builder.Build(); }
            catch (InvalidOperationException ex) { threw = true; message = ex.Message; }
            Assert(threw, "client Build() throws an InvalidOperationException when no transport is registered");
            Assert(message != null && message.Contains("No transport registered"),
                "the client no-transport Build() error names the missing transport registration");
            return Task.CompletedTask;
        }

        private Task NewApiClientBuildsWithRootsCapability()
        {
            // T15: a client built via the new API from a registered ITransport plus an AddRootsCapability(controller)
            // resolves into a non-null IClient. Build() is I/O-free (Connect() opens the wire), so registering an
            // unstarted in-memory transport end is safe in-process. The controller is registered directly and
            // pulled null-tolerantly in Build() — no IXCapabilityFactory indirection.
            var (clientEnd, _) = InMemoryTransport.CreatePair(Json, Loggers);
            var builder = new McpSdk.Client.ClientBuilder("n", "1.0");
            builder.Context.AddSingleton<ITransport>(clientEnd);
            McpSdk.Client.ClientContextExtensions.AddRootsCapability(builder.Context, new StubRootsController());

            var client = builder.Build();
            Assert(client != null,
                "new-API client Build() with a transport + AddRootsCapability(controller) returns a non-null IClient");
            return Task.CompletedTask;
        }

        private static async Task<List<string>> ListToolNames(IToolsController controller)
        {
            var result = await controller.ListTools(new ListToolsRequest(), null);
            return result.Tools.Select(t => t.Name).ToList();
        }

        /// <summary>A minimal tool handler with a caller-supplied name, used to populate composite leaves.</summary>
        private sealed class NamedToolHandler : IToolHandler
        {
            public Tool Tool { get; }

            public NamedToolHandler(string name) => Tool = new Tool(name, "test tool", new ObjectSchema());

            public Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
                => Task.FromResult(new CallToolResult(new Content[] { new TextContent("ok") }, false));
        }

        /// <summary>A registered dependency injected into <see cref="DependentToolHandler"/> to prove activation.</summary>
        private sealed class ToolDependency
        {
            public ToolDependency(string value) => Value = value;
            public string Value { get; }
        }

        /// <summary>A handler whose constructor requires a <see cref="ToolDependency"/>; its tool name embeds the injected value.</summary>
        private sealed class DependentToolHandler : IToolHandler
        {
            public Tool Tool { get; }

            public DependentToolHandler(ToolDependency dependency)
                => Tool = new Tool("dep-tool-" + dependency.Value, "needs a dependency", new ObjectSchema());

            public Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
                => Task.FromResult(new CallToolResult(new Content[] { new TextContent("ok") }, false));
        }

        /// <summary>A minimal one-tool <see cref="IToolsController"/> for the controller-overload aggregation check.</summary>
        private sealed class SingleToolController : IToolsController
        {
            private readonly Tool _tool;

            public SingleToolController(string name) => _tool = new Tool(name, "single", new ObjectSchema());

            // Never raised; empty accessors satisfy the interface without a CS0067 unused-event warning.
            public event Action ListChanged { add { } remove { } }
            public bool IsListChangedNotificationSupported => false;

            public Task<ListToolsResult> ListTools(ListToolsRequest request, McpRequestContext context)
                => Task.FromResult(new ListToolsResult(new[] { _tool }, null));

            public Task<CallToolResult> CallTool(CallToolRequest request, McpRequestContext context)
                => Task.FromResult(new CallToolResult(new Content[] { new TextContent("ok") }, false));
        }

        /// <summary>A minimal client-side roots controller for the new-API client Build() check.</summary>
        private sealed class StubRootsController : McpSdk.Client.IRootsController
        {
            // Never raised; empty accessors satisfy the interface without a CS0067 unused-event warning.
            public event Action ListChanged { add { } remove { } }
            public bool IsListChangedNotificationSupported => false;

            public Task<ListRootsResult> ListRoots()
                => Task.FromResult(new ListRootsResult(Array.Empty<Root>()));
        }
    }
}
