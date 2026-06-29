# McpSdk

[![McpSdk.Server](https://img.shields.io/nuget/v/McpSdk.Server?label=McpSdk.Server)](https://www.nuget.org/packages/McpSdk.Server)
[![McpSdk.Client](https://img.shields.io/nuget/v/McpSdk.Client?label=McpSdk.Client)](https://www.nuget.org/packages/McpSdk.Client)
[![CI](https://github.com/Zeejfps/EnvMcp/actions/workflows/ci.yml/badge.svg)](https://github.com/Zeejfps/EnvMcp/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Zeejfps/EnvMcp/blob/main/LICENSE)

A lean C# SDK for the [Model Context Protocol](https://modelcontextprotocol.io) — build MCP **clients**
and **servers** in .NET over stdio or Streamable HTTP. The core has zero third-party dependencies;
JSON and logging are pluggable adapters you opt into. Targets `netstandard2.0` and `net10.0`.

It implements the **2025-11-25** protocol revision and negotiates down to older peers (`2025-06-18`,
`2025-03-26`, `2024-11-05`).

## Features

- **Servers and clients** with a small, fluent builder API.
- **Two transports** — stdio and Streamable HTTP (single endpoint, sessions, SSE, `Last-Event-ID` resumption).
- **Tools** with JSON Schema 2020-12 input/output schemas, structured output, titles, annotations, and icons.
- **Prompts, resources** (with subscriptions), **completion**, and **logging** capabilities.
- **Client-side sampling, roots, and elicitation** for server→client requests.
- **Pagination, progress, cancellation, and `_meta` passthrough** throughout.
- **Pluggable JSON** — `System.Text.Json` or `Newtonsoft.Json` — and logging adapters.

## Installation

The SDK is split into small packages so you only pull what you need. Pick **Server** or **Client**, add a
**JSON adapter**, plus any transport/logging adapters. `McpSdk.Protocol` and `McpSdk.Shared` come in transitively.

```sh
# Build a server
dotnet add package McpSdk.Server
dotnet add package McpSdk.Adapter.System.Text.Json

# Build a client
dotnet add package McpSdk.Client
dotnet add package McpSdk.Adapter.System.Text.Json
```

| Package | What it's for |
|---|---|
| [`McpSdk.Server`](https://www.nuget.org/packages/McpSdk.Server) | Build MCP servers |
| [`McpSdk.Client`](https://www.nuget.org/packages/McpSdk.Client) | Connect to MCP servers |
| [`McpSdk.Adapter.System.Text.Json`](https://www.nuget.org/packages/McpSdk.Adapter.System.Text.Json) | JSON via `System.Text.Json` (`SystemJson`) |
| [`McpSdk.Adapter.Newtonsoft.Json`](https://www.nuget.org/packages/McpSdk.Adapter.Newtonsoft.Json) | JSON via Newtonsoft.Json / Json.NET (`NewtonsoftJson`) |
| [`McpSdk.Adapter.System.Net.Http`](https://www.nuget.org/packages/McpSdk.Adapter.System.Net.Http) | Streamable HTTP client transport (`StreamableHttpClient`) |
| [`McpSdk.Adapter.StreamableHttpServer`](https://www.nuget.org/packages/McpSdk.Adapter.StreamableHttpServer) | Streamable HTTP server transport (`StreamableHttpListener`) |
| [`McpSdk.Adapter.ConsoleLogger`](https://www.nuget.org/packages/McpSdk.Adapter.ConsoleLogger) | Console logging |
| [`McpSdk.Protocol`](https://www.nuget.org/packages/McpSdk.Protocol) | Core protocol types (transitive) |
| [`McpSdk.Shared`](https://www.nuget.org/packages/McpSdk.Shared) | Shared abstractions (transitive) |

> Swapping JSON engines is a one-line change: use `NewtonsoftJson` from `McpSdk.Adapter.Newtonsoft.Json`
> anywhere `SystemJson` appears below — both implement `IJson`.

## Quick start

### Server

Expose a tool over stdio:

```csharp
using McpSdk.Adapter.System.Text.Json;
using McpSdk.Server;

var json = new SystemJson();

var server = new ServerBuilder()
    .WithName("Demo Server")
    .WithVersion("1.0.0")
    .WithStdioTransport(json)
    .WithDefaultToolsCapability(json, tools => tools.AddTool(new EchoTool()))
    .Build();

await server.Start();
```

A tool is any `IToolHandler` — it advertises a `Tool` (name, description, JSON Schema) and handles the call:

```csharp
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Server;

public sealed class EchoTool : IToolHandler
{
    public Tool Tool { get; } = new Tool(
        "echo",
        "Echoes back the text you send.",
        new ObjectSchema
        {
            { "text", new StringSchema { Description = "Text to echo back" } },
        });

    public Task<CallToolResult> Call(IJsonObject args, McpRequestContext context)
    {
        var text = args["text"].AsString();
        var result = new CallToolResult([new TextContent($"You said: {text}")], isError: false);
        return Task.FromResult(result);
    }
}
```

### Client

Launch a server process and call its tools over stdio:

```csharp
using McpSdk.Adapter.System.Text.Json;
using McpSdk.Client;
using McpSdk.Client.Transports;
using McpSdk.Protocol.Models;

var json = new SystemJson();

var client = new ClientBuilder()
    .WithName("Demo Client")
    .WithVersion("1.0.0")
    .WithStdioTransport(json, "dotnet", ["run", "--project", "DemoServer"])
    .Build();

await client.Connect();

var tools = await client.ListTools();
foreach (var tool in tools.Tools)
    Console.WriteLine(tool.Name);

var request = new CallToolRequest("echo", json.Parse(json.Stringify(props =>
{
    props.Write("text", "hello");
})));
var result = await client.CallTool(request);
```

## Streamable HTTP

A single MCP endpoint over HTTP. The listener issues an `Mcp-Session-Id` on `initialize` and serves one
server per session; subsequent requests carry that id and the `MCP-Protocol-Version` header. Disallowed
`Origin`s are rejected with `403`, and the server→client `GET` stream supports `Last-Event-ID` resumption.

```csharp
// Server
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Adapter.System.Text.Json;
using McpSdk.Server;

var json = new SystemJson();
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
            .WithDefaultToolsCapability(json, tools => tools.AddTool(new EchoTool()))
            .Build();
        await server.Start();
    });

await listener.Start();
```

```csharp
// Client
using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.System.Net.Http;
using McpSdk.Adapter.System.Text.Json;
using McpSdk.Client;

var json = new SystemJson();
var loggerFactory = new ClientConsoleLoggerFactory();
var http = new StreamableHttpClient("http://localhost:3000/mcp", loggerFactory);

var client = new ClientBuilder()
    .WithName("Demo Client")
    .WithVersion("1.0.0")
    .WithLogger(loggerFactory)
    .WithStreamableHttpTransport(json, http)
    .Build();

await client.Connect();
```

## Capabilities

What the SDK serves as of the `2025-11-25` revision. ✅ supported · ❌ not implemented (deliberate scope choice).

### Protocol & transport

| Area | Support |
|---|---|
| Protocol revision | ✅ `2025-11-25` (latest), negotiated per-handshake |
| Back-compat (negotiated) | ✅ `2025-06-18`, `2025-03-26`, `2024-11-05` |
| stdio transport | ✅ client + server |
| Streamable HTTP transport | ✅ client + server — single endpoint, `Mcp-Session-Id`, `Origin`→`403`, `MCP-Protocol-Version` header, server→client SSE stream, `Last-Event-ID` resumption, `DELETE` lifecycle |
| OAuth 2.1 authorization | ❌ out of scope (fine for stdio / trusted-network; a public HTTP server normally needs it) |

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

### Tools & content

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

## License

Licensed under the [MIT License](LICENSE).
