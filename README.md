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
| Tools | `tools/list`, `tools/call` | `WithToolsCapability` / `WithDefaultToolsCapability` |
| Prompts | `prompts/list`, `prompts/get` | `WithPromptsCapability` |
| Resources | `resources/list`, `resources/read`, `resources/templates/list` | `WithResourcesCapability` |
| Resource subscriptions | `resources/subscribe`, `resources/unsubscribe` | `WithResourcesCapability` (when subscribe is supported) |
| Completion | `completion/complete` | `WithCompletionCapability` |
| Logging | `logging/setLevel` | `WithLoggingCapability` |

Server-emitted notifications: `notifications/message`, `notifications/progress`,
`notifications/cancelled`, `notifications/tools/list_changed`, `notifications/prompts/list_changed`,
`notifications/resources/list_changed`, `notifications/resources/updated`.

### Client — server→client requests handled

| Capability | Methods handled | Enable with |
|---|---|---|
| Base protocol | `ping`; any unknown method → `MethodNotFound` | always on |
| Roots | `roots/list` | `WithRootsCapability` |
| Sampling | `sampling/createMessage` (incl. `tools` + `toolChoice`) | `WithSamplingCapability` |
| Elicitation | `elicitation/create` (form + URL modes) | `WithElicitationCapability` |

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

## Examples

### Client Example

```C#
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Client;
using McpSdk.Protocol;

var json = new NewtonsoftJson();
var rootsCapabilityFactory = new RootsCapabilityFactory(json);
var samplingCapabilityFactory = new SamplingCapabilityFactory(json);

var client = new ClientBuilder(json)
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithStdioTransport("bun", ["index.ts"])
    .WithRootsCapability(rootsCapabilityFactory)
    .WithSamplingCapability(samplingCapabilityFactory)
    .Build();

await client.Connect();
```

### Server Example

```C#
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Server;
using McpSdk.Server.Tests;

var json = new NewtonsoftJson();
var schemaValidator = new NewtonsoftJsonSchemaValidator();
var mcpServer = new ServerBuilder()
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithConsoleLogger()
    .WithStdioTransport(json)
    .WithDefaultToolsCapability(json, schemaValidator, tools =>
    {
        tools.AddTool(new TestTool());
    })
    .Build();

await mcpServer.Start();
```

### Streamable HTTP (2025-11-25)

A single MCP endpoint over HTTP. The listener issues an `Mcp-Session-Id` on `initialize` and serves
one `McpServer` per session; subsequent requests carry that id and the `MCP-Protocol-Version` header.
Disallowed `Origin`s are rejected with `403`, and the server→client `GET` stream supports
`Last-Event-ID` resumption.

```C#
// Server
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Server;

var json = new NewtonsoftJson();
var schemaValidator = new NewtonsoftJsonSchemaValidator();
var loggerFactory = new ServerConsoleLoggerFactory();
var listener = new StreamableHttpListener(
    "http://localhost:3000", "/mcp", json, loggerFactory,
    onSession: async transport =>
    {
        var server = new ServerBuilder()
            .WithName("Demo Server")
            .WithVersion("1.0.0")
            .WithLogger(loggerFactory)
            .WithStreamableHttpTransport(transport)
            .WithDefaultToolsCapability(json, schemaValidator, tools => tools.AddTool(new TestTool()))
            .Build();
        await server.Start();
    });

await listener.Start();
```

```C#
// Client
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpClient;
using McpSdk.Client;

var json = new NewtonsoftJson();
var loggerFactory = new ClientConsoleLoggerFactory();
var http = new StreamableHttpClientAdapter("http://localhost:3000/mcp", loggerFactory);

var client = new ClientBuilder()
    .WithName("Echo Client")
    .WithVersion("1.0.0")
    .WithLogger(loggerFactory)
    .WithStreamableHttpTransport(json, http)
    .Build();

await client.Connect();
```
