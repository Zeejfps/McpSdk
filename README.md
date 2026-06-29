# McpSdk

A lean, zero-dependency C# SDK for the [Model Context Protocol](https://modelcontextprotocol.io). It
implements the **2025-11-25** revision and negotiates down to older peers (`2025-06-18`,
`2025-03-26`, `2024-11-05`). Both client and server ship from this repo, over stdio or Streamable
HTTP.

## Capability & feature matrix

Reflects what the code actually serves as of the `2025-11-25` revision. ✅ supported · ❌ not
implemented (by deliberate scope choice).

### Protocol & transport

| Area | Support |
|---|---|
| Protocol revision | ✅ `2025-11-25` (latest), negotiated per-handshake |
| Back-compat (negotiated) | ✅ `2025-06-18`, `2025-03-26`, `2024-11-05` |
| Request IDs | ✅ string or number |
| stdio transport | ✅ client + server |
| Streamable HTTP transport | ✅ client + server — single endpoint, `Mcp-Session-Id`, `Origin`→`403`, `MCP-Protocol-Version` header, server→client SSE stream, `Last-Event-ID` resumption, `DELETE` lifecycle |
| Legacy HTTP+SSE transport | ❌ removed |
| JSON-RPC batching | ❌ removed from the spec in `2025-06-18` |
| OAuth 2.1 authorization | ❌ out of scope (fine for stdio / trusted-network; a public HTTP server normally needs it) |
| Experimental Tasks | ❌ out of scope |

### Server — methods served

| Capability | Methods | Enable with |
|---|---|---|
| Base protocol | `initialize`, `ping` | always on |
| Tools | `tools/list`, `tools/call` | `Context.AddToolsCapability(...)` |
| Prompts | `prompts/list`, `prompts/get` | `Context.AddPromptsCapability(controller)` |
| Resources | `resources/list`, `resources/read`, `resources/templates/list` | `Context.AddResourcesCapability(controller)` |
| Resource subscriptions | `resources/subscribe`, `resources/unsubscribe` | `Context.AddResourcesCapability(controller)` (when subscribe is supported) |
| Completion | `completion/complete` | `Context.AddCompletionCapability(controller)` |
| Logging | `logging/setLevel` | `Context.AddLoggingCapability()` |

Server-emitted notifications: `notifications/message`, `notifications/progress`,
`notifications/cancelled`, `notifications/tools/list_changed`, `notifications/prompts/list_changed`,
`notifications/resources/list_changed`, `notifications/resources/updated`.

### Client — server→client requests handled

| Capability | Methods handled | Enable with |
|---|---|---|
| Base protocol | `ping`; any unknown method → `MethodNotFound` | always on |
| Roots | `roots/list` | `Context.AddRootsCapability(controller)` |
| Sampling | `sampling/createMessage` (incl. `tools` + `toolChoice`) | `Context.AddSamplingCapability(controller)` |
| Elicitation | `elicitation/create` (form + URL modes) | `Context.AddElicitationCapability(controller)` |

The client surfaces inbound `notifications/message` as `LogMessageReceived` and
`notifications/progress` as `ProgressReceived`.

### Tools & content features

| Feature | Support |
|---|---|
| Tool `inputSchema` / `outputSchema` (JSON Schema 2020-12 default dialect) | ✅ |
| Structured tool output (`structuredContent`, mirrored to a text block for back-compat) | ✅ |
| Tool `title`, `annotations`, `icons`, `_meta` | ✅ |
| Validation failures returned as tool errors (`isError`), not protocol errors | ✅ |
| Cursor-based pagination on every list op | ✅ |
| Content types | text, image, audio, embedded resource, `resource_link`, `tool_use`, `tool_result` — plus verbatim passthrough of unmodeled types |
| Elicitation schemas | primitives with defaults; `EnumSchema` (titled/untitled × single/multi-select) |
| Sampling | model preferences, tool-calling (`tools` + `toolChoice`), single-or-array content |
| Base-protocol utilities | `ping`, cancellation (`notifications/cancelled`), progress (`progressToken`), `_meta` passthrough |
| `Implementation` metadata | `name`, `version`, `title`, `description` |

## Construction model

The SDK builds a server or client through a small dependency-injection container exposed as
`IContext`. There is **one builder per role** — `new ServerBuilder(name, version)` /
`new ClientBuilder(name, version)` — whose `name` and `version` are **required constructor
parameters** (the code won't compile without them; you construct the builder directly, with no static
factory method).
Everything else — serializer, logger, transport, and capabilities — is registered on
`builder.Context` via `AddX(...)` extension methods, each contributed by the relevant adapter or
core assembly:

- **Serializer** (required): `Context.AddNewtonsoftJson()` (`McpSdk.Adapter.Newtonsoft.Json`) or
  `Context.AddSystemTextJson()` (`McpSdk.Adapter.System.Text.Json`).
- **Logger** (optional): `Context.AddConsoleLogger()` (from the `McpSdk.Adapter.ConsoleLogger`
  assembly, but reached through the builder's own `using McpSdk.Server;` / `using McpSdk.Client;` —
  see the deviations note below) or `Context.AddLogger(loggerFactory)` (`McpSdk.Shared`). Default is a
  no-op `NullLoggerFactory`.
- **Transport** (required): `Context.AddStdioTransport(...)` or `Context.AddStreamableHttpTransport(...)`.
- **Metadata** (optional): `Context.ConfigureInfo(info => { info.Title = ...; info.Description = ...; })`.
- **Capabilities**: every one follows the same `Context.Add<Name>Capability(...)` shape.

`Build()` is **synchronous** and validates the wiring (a missing serializer *or* transport throws
here, not later); it returns an `IServer` / `IClient`. `Start()` (server) and `Connect()` (client)
are **asynchronous** because they do I/O.

## Examples

### Server over stdio

```C#
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Server;

var builder = new ServerBuilder("Demo Server", "1.0.0");

builder.Context
    .AddNewtonsoftJson()                          // serializer (required)
    .AddConsoleLogger()                           // server overload: writes to stderr only
    .AddStdioTransport()                          // transport (required; Build() throws if none)
    .ConfigureInfo(info =>                         // optional metadata (name/version are already set)
    {
        info.Title       = "Demo";
        info.Description = "A demo MCP server";
    })
    .AddToolsCapability(tools => tools
        .AddTool(new EchoTool())                  // a tool instance you constructed
        .AddTool<WeatherTool>());                 // a tool the container constructs (ctor deps injected)

var server = builder.Build();                     // validates wiring
await server.Start();
```

`EchoTool` / `WeatherTool` implement `McpSdk.Server.IToolHandler`. The tools builder also exposes
`WithPageSize(int)` to set the `tools/list` page size (omit it for a single, unpaginated page). The
server-side `AddConsoleLogger()` (selected by `using McpSdk.Server;`) writes only to stderr because
the stdio transport owns stdout for protocol frames.

### Client over stdio

```C#
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;

var builder = new ClientBuilder("Echo Client", "1.0.0");

builder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()                           // client overload (selected by `using McpSdk.Client;`)
    .AddStdioTransport("bun", "index.ts")        // command to launch (+ optional arguments)
    .AddRootsCapability(new MyRootsController())
    .AddSamplingCapability(new MySamplingController());

var client = builder.Build();
await client.Connect();
```

Controllers implement `McpSdk.Client.IRootsController` / `ISamplingController` /
`IElicitationController` and are passed directly to `Add<Name>Capability(...)`.

### Streamable HTTP (2025-11-25)

A single MCP endpoint over HTTP. The host issues an `Mcp-Session-Id` on `initialize` and serves
**one `McpServer` per session**; subsequent requests carry that id and the `MCP-Protocol-Version`
header. Disallowed `Origin`s are rejected with `403`, and the server→client `GET` stream supports
`Last-Event-ID` resumption.

Configuration has two surfaces:

- **`Context`** — registered once and **shared by every session** (serializer, logger, stateless
  tools).
- **`ConfigureSession`** — a per-session callback on the transport options. Its `session` argument
  gives you a per-session `Context` (a child container) plus the connection's identity
  (`SessionId`, `Origin`, `Transport`), so behavior can vary per client. Per-session tools
  **aggregate** with the shared ones.

> `baseUrl` (host:port to bind, e.g. `http://localhost:3000`) and `path` (the endpoint route, e.g.
> `/mcp`) are separate parameters — don't put the path in the base url.

```C#
// Server
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Server;

var builder = new ServerBuilder("Demo Server", "1.0.0");

// Shared by every session
builder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .ConfigureInfo(info => info.Title = "Demo")
    .AddToolsCapability(tools => tools.AddTool(new EchoTool()));

// The transport. ConfigureSession runs once per session; session.Context is that session's
// own (child) registration surface, layered over the shared Context above.
builder.Context.AddStreamableHttpTransport(
    "http://localhost:3000",                      // baseUrl: host:port to bind
    "/mcp",                                       // path: endpoint route
    http => http.ConfigureSession(session =>
    {
        session.Context.AddToolsCapability(tools => tools.AddTool(new CartTool()));   // per-connection
        if (session.Origin == "https://admin.example.com")                            // only this origin
            session.Context.AddToolsCapability(tools => tools.AddTool(new AdminTool()));
    }));

var httpServer = builder.Build();
await httpServer.Start();                          // accepts connections; one McpServer per session
```

```C#
// Client
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpClient;
using McpSdk.Client;

var builder = new ClientBuilder("Echo Client", "1.0.0");

builder.Context
    .AddNewtonsoftJson()
    .AddConsoleLogger()
    .AddStreamableHttpTransport("http://localhost:3000/mcp");   // the full endpoint url (base + path)

var client = builder.Build();
await client.Connect();
```

## API notes & deviations from `API_DESIGN.md`

The public construction API is the one described in [`API_DESIGN.md`](./API_DESIGN.md). The shipped
surface deviates from that spec in the following ways:

- **`IServer.Log` on the Streamable HTTP host throws `NotSupportedException`.** Logging is advertised
  and served **per session**, and a multi-session HTTP host has no single session to log through, so
  its `IServer.Log(...)` throws. To emit a `notifications/message`, call `Log` on the per-session
  `McpServer` from inside the session (it owns one client). The **stdio** host (and the in-memory
  single-session host) has exactly one session, so its `Log` simply **delegates to that session** and
  works as documented.
- **`AddConsoleLogger()` is two namespace-scoped overloads, not one method.** The server and client
  console loggers differ (the server factory must avoid stdout, which the stdio transport owns), and a
  single `AddConsoleLogger(IContext)` can't tell a server build from a client build. So there are two
  overloads with identical signatures: `using McpSdk.Server;` selects the stderr-only
  `ServerConsoleLoggerFactory`; `using McpSdk.Client;` selects the `ClientConsoleLoggerFactory`. Your
  existing builder `using` picks the right one; never import both in the same file.
- **No static builder-factory method on `McpServer` / `McpClient`.** The concrete `McpServer` /
  `McpClient` keep their names and stay `internal`; you construct `ServerBuilder` / `ClientBuilder`
  directly with their `(name, version)` constructors rather than through any `McpServer.`-style static
  factory. (This was an explicit design choice in `API_DESIGN.md`, restated here because it is the
  discoverability trade-off.)
- **`ConfigureInfo(...)` receives an interface, not the options class.** The callback parameter is an
  `IServerInfoConfigurator` / `IClientInfoConfigurator` exposing only the settable `Title` /
  `Description` (the spec showed `Action<ServerInfoOptions>`). `Name` / `Version` are deliberately
  absent from the configurator — they are the required builder ctor parameters and cannot be changed
  here, so the signature tells the truth about what is mutable.
