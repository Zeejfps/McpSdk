# MCP Modernization: `2024-11-05` → `2025-11-25`

Migration plan to bring this hand-rolled C# MCP SDK up to the
[2025-11-25 specification](https://modelcontextprotocol.io/specification/2025-11-25).

## Scope decisions

| Decision | Choice | Rationale |
|---|---|---|
| Build vs. adopt | **Keep hand-rolled** | Stay zero-dependency. The official `ModelContextProtocol` SDK pulls in `Microsoft.Extensions.AI.Abstractions` + DI in its core, and full ASP.NET Core for the HTTP server. Our design is leaner. |
| Conformance | **Core + elicitation** | Version negotiation, modern tools, pagination, string IDs, content types, elicitation, richer sampling. **Skip** OAuth 2.1 and experimental Tasks. |
| Transports | **stdio first, HTTP later** | Land stdio fully compliant; Streamable HTTP is a self-contained final phase. |
| Sides | **Both** | Client and server are both shipped from this repo. |

## Guiding principles

- **Zero new dependencies.** All new models/logic go through the existing `IJson` / `IJsonObject` / `IJsonWriter` abstraction.
- **Negotiate, don't pin.** Support a *range* of protocol versions so legacy and modern peers both interoperate.
- **Each phase is independently shippable** with its own conformance tests.

---

## Current state

This SDK implements **MCP `2024-11-05`** — the first protocol revision. The target is
**four revisions newer** (`2024-11-05` → `2025-03-26` → `2025-06-18` → `2025-11-25`).
The version is hardcoded in two places (`Client/McpClient.cs:102`, `Server/McpServer.cs:171`)
with a strict-equality check that rejects any other version.

It is a clean, zero-dependency design: a custom JSON abstraction with Newtonsoft +
System.Text.Json adapters, stdio + the legacy HTTP+SSE transport, and separate
client/server libraries.

### Gap analysis

| Area | Current | Needed for `2025-11-25` |
|---|---|---|
| **Transport** | Legacy HTTP+SSE (dual `/sse` GET + `/messages` POST — `HttpListenerSseServer.cs`) | **Streamable HTTP** (single endpoint, `Mcp-Session-Id`, resumable SSE, Origin→403, `MCP-Protocol-Version` header) |
| **Tools** | name/description/inputSchema; text+image+resource content | `outputSchema` + `structuredContent`, **annotations**, `title`, **icons**, resource-link + audio content |
| **Pagination** | None (`ListToolsResult` has no cursor) | Cursor-based pagination on all list ops |
| **Request IDs** | `int` only (`JsonRpcTransport` / `OnMessageReceived` call `AsInt()`) | string-or-number IDs |
| **Elicitation** | Absent | New client capability (+ URL mode, EnumSchema, multi-select) |
| **Sampling** | Basic | `tools` + `toolChoice` (tool-calling in sampling) |
| **Versioning** | Hardcoded + strict equality | Negotiated over a supported range |
| **Misc** | — | JSON Schema 2020-12 default, `_meta` everywhere, `Implementation.title`/`description`, validation errors as tool-errors not protocol-errors |

*Out of scope (this cycle): OAuth 2.1 authorization, experimental Tasks.*

---

## Phase A — Protocol foundation (version negotiation + JSON-RPC correctness)

The unblocker; everything else builds on it.

- [x] **Version constants.** New `Protocol/ProtocolVersion.cs`: `Latest = "2025-11-25"` and a
  `Supported` set `["2025-11-25","2025-06-18","2025-03-26","2024-11-05"]`. Deleted the hardcoded
  `"2024-11-05"` strings (`Client/McpClient.cs`, `Server/McpServer.cs`).
- [x] **Real negotiation** (replaced the strict-equality rejects):
  - *Server:* echoes the client's version if supported; otherwise returns `Latest`. Never errors on mismatch.
  - *Client:* sends `Latest`; if the server's reply isn't in `Supported`, calls `Stop()` and throws.
- [x] **String-or-number request IDs.** Introduced a `RequestId` struct (string or long) in
  `Protocol/RequestId.cs`, threaded through `ITransport.SendOkResponse/SendErrorResponse`, the
  `RequestReceived` callback, `JsonRpcTransport` (now keyed on `Dictionary<RequestId,…>`), and both
  `McpServer`/`McpClient` handlers (plus `McpTransportBridge`). Added `IJsonProperty.IsString`/`AsLong`
  and `IJsonWriter.Write(long)` to the JSON abstraction (both adapters).
- [x] **Bug:** `McpClient` sent `"initialized"` — now `notifications/initialized`.
- [x] **Bug:** `InitializeResult(IJsonObject)` parsed *only* `protocolVersion` — now parses
  `capabilities` + `serverInfo` (also fixed `ClientInfo`/`ServerInfo` reading capitalized `"Name"`/`"Version"`).
- [x] **`Implementation` metadata.** Added optional `title` (2025-06-18) and `description` (2025-11-25)
  to `ClientInfo` / `ServerInfo`, wired through both builders (`WithTitle`/`WithDescription`).
- [x] **`_meta` passthrough.** Added a reusable `Protocol/Models/Meta.cs` primitive (opaque JSON
  object) with read/write applied to the `initialize` request + result; available for other models in
  later phases.

**Exit:** ✅ initialize handshake negotiates correctly with a `2025-11-25` peer *and* a legacy
`2024-11-05` peer; string-ID peers work. Verified by `Server.Tests/Conformance` (21 assertions,
run via `dotnet run --project Server.Tests -- conformance`).

---

## Phase B — stdio compliance pass

Small; mostly verification.

- [x] **Newline-delimited UTF-8 framing, no embedded newlines.** Centralized the per-message
  sanitization (previously a duplicated `Regex.Replace` in both transports) into
  `Shared/JsonRpcFraming.cs`: `ToLine()` strips any embedded CR/LF/TAB (escaped `\n`/`\r`/`\t` inside
  string values are untouched), `LineDelimiter = '\n'`. Both stdio transports now bind to the raw
  std handles with **UTF-8 (no BOM)** and **LF** line endings (client sets `ProcessStartInfo`
  `Standard*Encoding` on net10.0 + `NewLine="\n"`; server writes via
  `StreamWriter(Console.OpenStandardOutput(), utf8NoBom){ AutoFlush=true, NewLine="\n" }`).
- [x] **stdout carries only MCP messages.** Logging already goes to stderr; additionally the server
  stdio transport redirects `Console.Out → stderr` on start so stray `Console.Write` (e.g. from tool
  code) can never corrupt the protocol stream.
- [x] **No JSON-RPC batching** (removed in 2025-06-18). `JsonRpcFraming.IsBatch()` detects a
  top-level array; `JsonRpcTransport.OnMessageReceived` now rejects batch frames explicitly with a
  logged error instead of letting `JObject.Parse` throw and be swallowed. We never emit batches
  (each `Send` serializes a single object).
- [x] **Bug:** server `StdioTransport.OnStop` threw `NotImplementedException` — now cancels the
  stdin read loop cleanly.

**Exit:** ✅ passes a real stdio round-trip conformance test against the negotiated version — a real
client spawns the test assembly in `stdio-server` mode as a child process and completes
`initialize` + `tools/list` + `tools/call`. Verified by `Server.Tests/Conformance` (now 37
assertions: Phase A + B, run via `dotnet run --project Server.Tests -- conformance`).

---

## Phase C — Modern tools

The highest-value feature jump.

- [x] **`Tool` model** (`Protocol/Models/Tool.cs`): added `Title`, `OutputSchema` (`ObjectSchema`),
  `Annotations` (new `Protocol/Models/ToolAnnotations.cs`: `title`, `readOnlyHint`, `destructiveHint`,
  `idempotentHint`, `openWorldHint` — all optional nullable bools, emitted only when set), `Icons`,
  and `_meta`. The `Tool(IJsonObject)` reader parses all of them; `AsJson` emits each when present.
- [x] **Structured output** (`CallToolResult.cs`): added `StructuredContent` (opaque `IJsonObject`)
  and `_meta`. New `CallToolResult.Structured(structuredContent, …extra)` factory emits
  `structuredContent` **and** mirrors it into a leading serialized-JSON text block so clients that only
  read the unstructured `content` array still work (SEP).
- [x] **New content types** in `Content.Create` (`Protocol/Models/Content.cs`): `AudioContent`
  (`type:"audio"`, `Protocol/Models/AudioContent.cs`) and **resource links**
  (`type:"resource_link"`, `Protocol/Models/ResourceLinkContent.cs`: `uri`/`name`/`title`/
  `description`/`mimeType`).
- [x] **Validation errors → tool errors.** `DefaultToolsController.CallTool` already returned
  `CallToolResult { isError: true }` for schema-validation failures (never JSON-RPC `InvalidParams`);
  hardened it to treat omitted `arguments` as an empty object (so a missing-required-field call
  validates instead of null-reffing into `InternalError`) and documented the SEP-1303 intent.
- [x] **Bug:** the System.Text.Json adapter's `IJsonObject.IsValid` keyed off `result.HasErrors`,
  which is always false under JsonSchema.Net's default `Flag` output — so *every* object passed
  validation, voiding the tool-error guarantee on that adapter. Now evaluates with `List` output and
  keys off `IsValid`, collecting per-node error messages. Guarded by a cross-adapter parity test.
- [x] **Icons** shared model (`Protocol/Models/Icon.cs`: `src` / `mimeType` / `sizes`) with
  `ArrayFrom`/`WriteArray` helpers, ready to be reused by resources/prompts in Phase F.
- [x] **JSON Schema 2020-12** as default dialect: `JsonSchema.Dialect2020_12` constant, emitted as
  `$schema` on the root `ObjectSchema` (tool input/output). Both validators
  (`Newtonsoft.Json.Schema` 4.0.1, `JsonSchema.Net` 7.3.3) accept it.

**Exit:** ✅ structured-output round-trip (`structuredContent` + back-compat text), `title` +
annotations + icons + `$schema`/`outputSchema` appear in `tools/list`, schema-validation failures
return `isError` tool results, and audio/resource_link content round-trips. Verified by
`Server.Tests/Conformance` (now 70 assertions, run via `dotnet run --project Server.Tests -- conformance`).

---

## Phase D — Pagination

- [x] **Opaque `cursor` param + `nextCursor` result** across all four list ops. New request models
  carry an optional `cursor` (`ListToolsRequest`, `ListPromptsRequest`, and `cursor` added to the
  existing `ListResourcesRequest` / `ListTemplatesRequest`); every list result now exposes an
  optional `NextCursor` (`ListToolsResult`, `ListResourcesResult`, `ListTemplatesResult`,
  `ListPromptsResult`) — emitted only on a non-final page, read back when present.
- [x] **Cursors are opaque.** New `Protocol/PaginationCursor.cs` Base64-encodes the page offset
  behind an `offset:` token so clients echo a blob they can't parse; `TryDecodeOffset` rejects
  null/empty/malformed cursors rather than throwing, so a token a controller didn't mint degrades to
  the first page.
- [x] **Controllers accept/return cursors.** `IToolsController.ListTools` and
  `IPromptController.ListPrompts` now take their request models (resources already did); `McpServer`
  parses the incoming params into those models. `McpClient.ListTools` / `IClient.ListTools` take an
  optional `ListToolsRequest` (default null → first page, back-compatible with existing callers).
- [x] **Real paging in `DefaultToolsController`.** New settable `PageSize` (null = single page, no
  cursor). When set, `ListTools` snapshots a stable order, slices `[offset, offset+PageSize)`,
  clamps stale/out-of-range offsets to an empty final page, and returns a `nextCursor` only while
  more tools remain.

**Exit:** ✅ multi-page `tools/list` walks every page via `nextCursor`, returning each tool exactly
once and a null cursor on the final page; a non-paginating controller returns one page with no
cursor; the opaque cursor round-trips offsets and rejects junk. Verified by
`Server.Tests/Conformance` (now 91 assertions, run via `dotnet run --project Server.Tests -- conformance`).

---

## Phase E — Elicitation + richer sampling

- [x] **Elicitation** (new client capability). Server→client `elicitation/create` handled by a new
  branch in `McpClient.OnRequestReceived` (`McpClient.cs`), mirroring the roots/sampling wiring:
  new `Protocol/Models/ClientCapabilities/ElicitationCapabilityModel.cs` (advertises `form` and/or
  `url` modes; empty object ⇒ form-only per spec), `Client/IElicitationController.cs` (with
  `SupportsFormMode`/`SupportsUrlMode` driving the capability) + `IElicitationCapabilityFactory.cs`,
  `Protocol/Models/ElicitRequest.cs` and `ElicitResult.cs` (three-action `accept`/`decline`/`cancel`).
  Wired through `ClientCapabilitiesModel`, `ClientBuilder.WithElicitationCapability`, and the
  `McpClient` ctor. The client rejects a request whose mode it never advertised with `InvalidParams`
  (-32602) and an absent controller with `MethodNotFound` (-32601), instead of hanging.
- [x] **URL-mode elicitation** (2025-11-25). `ElicitRequest.ForUrl(message, url, elicitationId)` builds
  a `mode:"url"` request; `ElicitResult.AcceptUrl()` is a content-less consent. Form mode carries the
  restricted `requestedSchema`; URL mode carries `url` + `elicitationId`.
- [x] **`EnumSchema`** (`Protocol/Models/EnumSchema.cs`, SEP-1330): all four shapes — untitled/titled
  (`enum` vs `oneOf`/`anyOf` with `const`+`title`) × single/multi-select (scalar string vs `type:array`
  with `items`, `minItems`/`maxItems`). New `Protocol/Models/RequestedSchema.cs` is the flat
  elicitation object (primitive + enum properties, required/optional tracking) that round-trips on
  `ElicitRequest`.
- [x] **Primitive default values** (SEP-1034). Added `Default` (+ `Title`) to `StringSchema`
  (also `format`/`pattern`), `NumberSchema` (also an `IsInteger` ⇒ `type:"integer"`), and
  `BooleanSchema`; `EnumSchema` carries scalar (single) / array (multi) defaults.
- [x] **Sampling with tools** (SEP-1577). `CreateMessageRequest` gained optional `Tools` (reusing the
  `Tool` model) + `ToolChoice` (`Protocol/Models/ToolChoice.cs`: `auto`/`required`/`none`), plus a
  build constructor. New `ToolUseContent` (`type:"tool_use"`) and `ToolResultContent`
  (`type:"tool_result"`) content types, registered in `Content.Create`. `SamplingCapabilityModel`
  advertises the `tools` sub-capability; `ISamplingController` gained `SupportsTools`.
- [x] **Single-or-array content.** `SamplingMessage` / `CreateMessagesResult` now expose `Content[]`
  (matching `CallToolResult`) and emit a single object for one block, an array for many — both valid
  per spec. New `Content.CreateMany` reads either shape, backed by a new `IJsonProperty.IsArray`
  (implemented in both JSON adapters). **Bug:** `CreateMessagesResult` read the model from `"module"`
  instead of `"model"` — fixed.

**Exit:** ✅ elicitation accept/decline/cancel round-trip in both form and URL modes (with mode
negotiation and undeclared-mode rejection), the restricted `requestedSchema` — all four enum forms
plus primitive defaults — round-trips, and a tool-enabled `sampling/createMessage` carries
`tools`/`toolChoice` and returns `tool_use`/`tool_result` content. Verified by
`Server.Tests/Conformance` (now 167 assertions, run via `dotnet run --project Server.Tests -- conformance`).

---

## Phase F — Resources / prompts / completion polish

The prompt + resource-template models were unimplemented stubs (`ListPromptsResult`/`ListTemplatesResult`
carried only a cursor, `GetPromptResult` was empty, and no `Prompt`/`PromptMessage`/`ResourceTemplate`
classes existed), so this phase **builds them out** rather than merely augmenting them.

- [x] **New models** (`Protocol/Models/`): `Prompt` (`name`/`title`/`description`/`arguments`/`icons`/
  `_meta`), `PromptArgument` (`name`/`title`/`description`/`required`), `PromptMessage`
  (`role` + a single content block + `_meta`), and `ResourceTemplate` (`uriTemplate`/`name`/`title`/
  `description`/`mimeType`/`icons`/`_meta`). All follow the modern `Tool` pattern: nullable reads,
  optional fields emitted only when set, the `IJsonProperty.AsArray<T>` reader extension for object
  arrays, `Meta` for `_meta`.
- [x] **`icons` + `title` + `_meta`** added to `Resource` (also made its optional `description`/
  `mimeType` null-safe + conditionally written). `PromptMessage` carries only `_meta` by design —
  `title`/`icons` belong on the content block or parent `Prompt`, not on a message.
- [x] **Wired into the list/get results:** `ListPromptsResult` now carries `Prompt[]`,
  `ListTemplatesResult` carries `ResourceTemplate[]`, `GetPromptResult` carries `description` +
  `PromptMessage[]` + `_meta` (all previously dropped). `GetPromptRequest` (was a no-op stub) now
  reads/writes `name` + an opaque `arguments` map.
- [x] **Completion `context`** (2025-06-18). New `Server/CompletionContext.cs` (opaque resolved-
  variable `arguments` map) added to `CompletionRequest` — emitted only when supplied, read back when
  present.
- [x] **Bug:** `ResourcesCapabilityModel` wiring at `McpServer.cs:59` copied `listChanged` into the
  `subscribe`/resource-changed flag — now fed from `IsResourceChangedNotificationSupported`. Also
  hardened the model's reader to parse `subscribe` independently of `listChanged` (it previously
  ignored `subscribe` entirely).

**Exit:** ✅ `title`/`icons`/`_meta` round-trip on `Resource`, `ResourceTemplate`, `Prompt` (with its
`PromptArgument`s) and `PromptMessage` — and are omitted when absent; `prompts/list`, `prompts/get`
and `resources/templates/list` carry their item arrays; `prompts/get` requests round-trip
`name`+`arguments`; a completion request carries `context`; and a server advertises `subscribe` and
`listChanged` as the independent capabilities they are. Verified by `Server.Tests/Conformance` (now
218 assertions, run via `dotnet run --project Server.Tests -- conformance`).

---

## Phase G — Streamable HTTP transport

The final phase, and the whole point of the HTTP work: replace the legacy dual-endpoint
HTTP+SSE transport (`/sse` GET + `/messages` POST) with the spec's **single-endpoint Streamable
HTTP** transport. We do this in two movements — **first delete the legacy transport outright**
(no compatibility flag, no parallel path), **then build Streamable HTTP** on the cleared deck.
stdio is untouched throughout; it is not legacy and stays.

### G.1 — Remove the legacy HTTP+SSE transport ✅

Delete it wholesale — there are no production consumers, only the two `*.Tests` demo entry points,
which move to stdio (and later Streamable HTTP).

> The bulk of this was carried out in tandem with G.2 ("delete the legacy transport, then build
> Streamable HTTP on the cleared deck"), so by the time G.1 was revisited the code was already gone.
> A verification pass confirmed zero SSE/bridge code remains (no source files, no `.sln`/`.csproj`
> references, no GUIDs) and removed the last artifact — a stale, git-untracked `Adapter.SseServer/obj/`
> left on disk after its sources/`.csproj` were deleted.

- [x] **Deleted whole projects** (4): `Adapter.SseClient/`, `Adapter.SseServer/`,
  `StdioToSseBridge/`, `TransportBridge/`. The two bridges existed only to tunnel stdio↔legacy-SSE;
  `McpTransportBridge` had no other consumer once `StdioToSseBridge` was gone.
- [x] **Deleted SSE files from the core libs** (self-contained — the builders, `McpClient`, and
  `McpServer` never referenced them):
  - `Client/`: `SseTransport.cs`, `SseTransportFactory.cs`, `SseTransportClientBuilderExtensions.cs`,
    `ISseClient.cs`, `ISseClientFactory.cs`
  - `Server/`: `SseTransport.cs`, `SseTransportFactory.cs`, `SseEvent.cs`, `ISseServer.cs`,
    `ISseSession.cs`
- [x] **Updated references:**
  - `MCPSharp.sln` — the 4 `Project(...)` declarations and their `GlobalSection` config blocks
    (GUIDs `B0C6F17D…` SseClient, `386DEDC5…` SseServer, `73A33E46…` StdioToSseBridge,
    `14D62640…` TransportBridge) are gone.
  - `Client.Tests/Client.Tests.csproj` — no longer references `Adapter.SseClient`; references
    `Adapter.StreamableHttpClient` instead.
  - `Server.Tests/Server.Tests.csproj` — no longer references `Adapter.SseServer`; references the
    `Adapter.StreamableHttp{Server,Client}` projects instead.
  - `Client.Tests/Program.cs` — drives an `http(s)` URL over Streamable HTTP, or a stdio command
    otherwise (superseded the original "rewrite to stdio" interim plan once the G.2c client landed).
  - `Server.Tests/Program.cs` — the `HttpListenerSseServer` demo block is gone; it exposes
    `conformance`, `stdio-server`, and `streamable-http-server` modes.
  - `README.md` — carries no `using McpSdk.Adapter.SseServer;`.
- [x] **Stays put** (transport-neutral infra, not legacy): stdio (`Client|Server/StdioTransport*.cs`),
  `Protocol/ITransport.cs`, `Protocol/TransportExtensions.cs`, `Shared/JsonRpcTransport.cs`,
  `Shared/ITransportFactory.cs`, `Shared/TransportErrorException.cs`, and the in-memory test
  transport (`Server.Tests/Conformance/InMemoryTransport.cs`, `FixedTransportFactory.cs`) — all present.
- [x] **Gate:** `dotnet build MCPSharp.sln` is clean and `Server.Tests -- conformance` passes (now
  331 assertions, up from the 218 baseline noted when this phase was drafted) with zero SSE code
  remaining.

### G.2 — Streamable HTTP transport (the rebuild)

New transport that satisfies the `ITransport` contract directly (not via `JsonRpcTransport`): unlike
the symmetric legacy SSE channel, Streamable HTTP correlates each response to the POST that carried
its request, so the single-`Send` pump doesn't fit. It follows the old core-abstraction + adapter
split — transport in `Server`/`Client`, the concrete HTTP machinery in adapter projects. New
projects: `Adapter.StreamableHttpServer/` (`HttpListener`-based, zero new deps) and
`Adapter.StreamableHttpClient/` (`HttpClient`-based), with `WithStreamableHttpTransport` builder
extensions on each side. Built in three independently-verifiable increments.

#### G.2a — Core request/response, sessions, security ✅

- [x] **Single endpoint, request/response.** `POST` carries a JSON-RPC message; a request is answered
  on the same HTTP response with `application/json`, a notification/response with **202 Accepted**
  (no body). Client `Accept` lists both `application/json` and `text/event-stream`. The server
  transport (`StreamableHttpServerTransport`) correlates the response back to the originating POST via
  a per-request-id slot completed by `SendOkResponse`/`SendErrorResponse`; the client transport
  (`StreamableHttpClientTransport`) does synchronous POST→response with its own id counter.
- [x] **Sessions.** Server issues `Mcp-Session-Id` on the `initialize` response (the POST that has no
  session header bootstraps a session + per-session McpServer via the listener's awaited `onSession`
  callback); client echoes it on every subsequent request. An unknown/expired session → **404**.
- [x] **Version header.** `MCP-Protocol-Version` required on all post-`initialize` requests (→ **400**
  if absent); the client captures the negotiated version from the `initialize` result and replays it.
- [x] **Security.** `Origin` validated against an allow-list → **HTTP 403** (DNS-rebinding guard);
  absent `Origin` (non-browser clients) is permitted.
- [x] **Conformance.** In-process `Server.Tests/Conformance/PhaseGConformanceTests.cs`: a real
  listener + real HTTP client run the full `initialize` → `tools/list` → `tools/call` round-trip over
  one endpoint, plus raw-HTTP checks for the session id, `Origin`→403, the version-header 400, and the
  unknown-session 404. Suite now **231 assertions** (was 218).

#### G.2b — SSE streaming + server→client ✅

- [x] **Standalone GET stream.** A `GET` to the endpoint (session id + version headers) opens the
  server→client `text/event-stream`; the listener attaches it to the session transport and holds it
  open with cancellable comment heartbeats. Each frame is an SSE `id:`/`data:` event. (POST requests
  stay `application/json`; routing all server→client traffic over the GET stream is a valid server
  profile, so the optional "POST answered with an SSE stream" path is intentionally not implemented.)
- [x] **Server-initiated requests/notifications.** `StreamableHttpServerTransport.SendNotification`
  pushes onto the stream; `SendRequest` (formerly threw) assigns an id, pushes, and awaits the
  client's reply — which arrives as a separate client POST and is correlated back. The client transport
  opens the GET stream after initialize and dispatches inbound frames to `RequestReceived` /
  `NotificationReceived` (driving sampling / elicitation / logging / `*_list_changed`).
- [x] **Conformance.** A transport-level server→client round-trip over real HTTP: a notification
  reaches the client, and a server→client request is answered by the client and correlated back to the
  server. Suite **235 assertions**.

#### G.2c — Resumability, lifecycle, wiring ✅

- [x] **Resumability.** The server transport stamps each server→client event with a monotonic id and
  retains a bounded ring (256) for replay; a `GET` carrying `Last-Event-ID` replays only the missed
  tail. The client tracks the last event id and reconnects (resuming) if the stream drops.
- [x] **Lifecycle.** Client `DELETE` terminates the session — the listener drops it and `Terminate()`s
  the transport (detaches the stream, cancels in-flight requests, closes the GET via a linked token);
  subsequent requests on it → **404**. The client sends `DELETE` on `Stop()`.
- [x] **Demo wiring.** `Server.Tests/Program.cs` gained a `streamable-http-server [baseUrl]` mode
  (one endpoint, per-session McpServer); `Client.Tests/Program.cs` drives an `http(s)` URL over
  Streamable HTTP (or a stdio command otherwise). Verified cross-process end-to-end.
- [x] **Conformance.** Raw-HTTP `Last-Event-ID` replay (only the missed tail, skipping seen events) and
  `DELETE`→200→404 lifecycle. Suite **244 assertions** (was 218 at the end of Phase F).

**Exit:** ✅ legacy HTTP+SSE is gone; a Streamable HTTP client and server complete the full
`initialize`/`tools` round-trip over a single endpoint with session ids, the protocol-version header,
`Origin`→403, server→client streaming, and `Last-Event-ID` resumption — verified by the Phase G
conformance suite (244 assertions) and a cross-process demo (`Server.Tests` ↔ `Client.Tests`).

---

## Phase H — Base-protocol & utility completeness

Phases A–G were organized around the **changelog** — what each revision *added*. That lens missed
the long-stable parts of the spec that never changed and so never appeared in a changelog: the
base-protocol **utilities** (`ping`, cancellation, progress) and several server **utility methods**
(`logging/setLevel`, `completion/complete`, resource subscriptions). A few of these were *modelled*
in earlier phases but never *wired* — e.g. the full completion model set and `ICompletionController`
exist, yet `completion/complete` is not in the server's handler table; `ResourcesCapabilityModel`
can advertise `subscribe: true`, but no `resources/subscribe` handler exists. Advertising a
capability the server can't service is an active conformance violation, so this phase closes the
gap between *modelled* and *served*.

Two cross-cutting bugs motivate doing it now:

- **Client hangs on any unknown server→client request.** `McpClient.OnRequestReceived` handles only
  `roots/list` / `sampling/createMessage` / `elicitation/create` and **silently drops** everything
  else — no response at all. A server→client `ping` (or anything new) never returns. The server
  correctly replies `MethodNotFound`; the client must too.
- **Capabilities advertised but unserved** (`completion`, `resources.subscribe`, `logging`).

### H.1 — Ping + unknown-method correctness (base protocol)

- [x] **`ping` responder on both sides.** Either party MAY send `ping`; the receiver MUST reply
  promptly with an empty result `{}`. Registered `ping` in `McpServer` and `McpClient`.
- [x] **Client unknown-method → `MethodNotFound`.** `McpClient.OnRequestReceived` returns a
  `-32601` error for any method it doesn't handle, instead of dropping it (fixes the hang).
- [x] **`IClient.Ping()`** convenience to actively ping the server (the responder is the compliance
  requirement; this makes it usable + testable).

### H.2 — Cancellation (`notifications/cancelled`)

- [x] **Canceller side.** When a `CancellationToken` passed to an outbound request fires, the sender
  emits `notifications/cancelled` (`requestId`) before unwinding. Wired in `McpClient.SendRequest`;
  `CallTool` / `ListTools` now take an optional `CancellationToken`.
- [x] **Receiver side.** `McpServer` tracks in-flight requests in a `ConcurrentDictionary`; an inbound
  `notifications/cancelled` cancels the matching request and **suppresses its response** (spec permits
  dropping it). Never errors on the notification, and ignores an unknown/already-finished id.
- [x] **Cooperative cancellation.** A per-request ambient `McpRequestContext` (an `AsyncLocal`
  carrying the `CancellationToken`) lets long-running controllers observe cancellation without a
  breaking signature change to the controller interfaces.

### H.3 — Progress (`notifications/progress`)

- [x] **Token capture.** `McpServer` reads `_meta.progressToken` (string or number) off an inbound
  request and exposes it on `McpRequestContext` (reusing the `RequestId` string-or-number type).
- [x] **Emit.** `McpRequestContext.ReportProgress(progress, total?, message?)` sends
  `notifications/progress` keyed to that token; a no-op when the caller supplied none.
- [x] **Inbound dispatch.** The client surfaces an inbound `notifications/progress` via an
  `IClient.ProgressReceived` event (the server logs it); neither side drops it. New
  `Protocol/Models/ProgressNotification.cs`.

### H.4 — Logging utility

- [x] **`LoggingLevel`** model (the eight RFC-5424 levels: `debug`…`emergency`), ordered so a
  single `(int)` compare filters by severity. New `Protocol/Models/LogMessage.cs` + `SetLevelRequest.cs`.
- [x] **`logging/setLevel` handler** in `McpServer`, gated on a new
  `ServerBuilder.WithLoggingCapability()`; advertises the `logging` capability only when enabled.
- [x] **Emit `notifications/message`.** `IServer.Log(level, data, logger?)` sends a structured log,
  filtered by the client's last-set level (a no-op below it, or when logging is disabled).
- [x] **Client inbound dispatch.** `McpClient` routes `notifications/message` to the
  `IClient.LogMessageReceived` event; `IClient.SetLoggingLevel(...)` sends `logging/setLevel`.

### H.5 — Completion wiring

- [x] **Register `completion/complete`** in `McpServer`, consuming the existing `ICompletionController`
  / `CompletionRequest` / `CompletionResult` models (previously dead code). Fixed their non-spec wire
  names while wiring: the request's single `argument` (was `arguments`), the result's `total` / `hasMore`
  (were `totalMatches` / `hasMoreMatches`), and nesting the result under `completion`.
- [x] **`ServerBuilder.WithCompletionCapability(...)`** advertises the `completion` capability and
  installs the controller. `completion/complete` returns `MethodNotFound` only when not configured.

### H.6 — Resource subscriptions + notifications

- [x] **`resources/subscribe` / `resources/unsubscribe` handlers** in `McpServer`, gated on the
  `resources.subscribe` capability (so advertise ⇒ serve).
- [x] **`notifications/resources/updated`** emitted (per-URI) and **`notifications/resources/list_changed`**
  emitted — the two resource notifications the server never sent (it only sent tools/prompts variants).
- [x] **Controller surface.** `IResourcesController` gains `Subscribe`/`Unsubscribe` + a per-URI
  `event Action<string> ResourceUpdated` (replacing the unused parameterless `ResourceChanged`),
  alongside its existing `ListChanged` event.

**Exit:** ✅ every method the server advertises is actually served; `ping` round-trips both directions
and an unknown server→client method returns `MethodNotFound`; `notifications/cancelled` cancels an
in-flight request; `notifications/progress` round-trips against a captured `progressToken`;
`logging/setLevel` + `notifications/message` work end-to-end (with severity filtering);
`completion/complete` returns suggestions; and `resources/subscribe`/`unsubscribe` + the
`updated`/`list_changed` notifications round-trip. Verified by the new
`Server.Tests/Conformance/PhaseHConformanceTests.cs` suite (full conformance run now **284
assertions**, up from 252; run via `dotnet run --project Server.Tests -- conformance`).

*Still out of scope (unchanged): OAuth 2.1 authorization, experimental Tasks. Note: "fully
spec-compliant" for a **public** Streamable HTTP server normally implies the Authorization spec —
fine to defer for stdio / trusted-network use, but a conscious choice, not an omission.*

---

## Cross-cutting

- **Tests:** extend `Server.Tests` / `Client.Tests` with a conformance case per phase (negotiation,
  pagination, structured output, elicitation).
- **Docs:** update `README.md` examples; add a capability/feature matrix.
- **Sequencing:** A → B → C → D → E → F can each merge independently; G next, and G itself is
  ordered — **G.1 deletes** the legacy HTTP+SSE transport (shippable on its own: stdio-only SDK),
  then **G.2 builds** Streamable HTTP. **H** closes the base-protocol/utility gaps and its
  sub-phases (H.1–H.6) are independent of one another — each is shippable on its own.

---

## Known gaps — missing implementations

What's *not* done. Surfaced while reorganizing the conformance suite from phase-named to
feature-named classes and adding end-to-end coverage for roots, resources, and prompts. Every MCP
method has a working handler and round-trips end-to-end. The two model-level gaps below are now
closed (suite **331 assertions**, up from 317).

### Resources — `resources/read` carries contents ✅

The method was routed and reached `IResourcesController.ReadResource`, but the request and result
models were stubs, so neither the requested URI nor the returned contents survived the wire. Both now
round-trip.

- [x] **`ReadResourceResult`** (`Protocol/Models/ReadResourceResult.cs`) — now carries
  `ResourceContents[] Contents` (+ opaque `_meta`), writes a `contents` array, and parses it back via
  the existing `ResourceContents.FromJsonObject` (handles `TextResourceContents` /
  `BlobResourceContents`). Added the `(ResourceContents[])` ctor.
- [x] **`ReadResourceRequest`** (`Protocol/Models/ReadResourceRequest.cs`) — now has a `Uri` property
  (parsed in the `IJsonObject` ctor, emitted by `WriteMembers`) and a `(string uri)` ctor.
  `McpServer.HandleReadResourceRequest` already passes the parsed request through, so the URI now
  reaches the controller.
- [x] *Test:* tightened `ResourcesTests.ReadResourceIsRouted` to assert the URI reaches the controller
  and that text + blob contents round-trip back to the client.

### Content — `UnknownContent` round-trips verbatim ✅

- [x] **`UnknownContent`** (`Protocol/Models/UnknownContent.cs`) — the catch-all for unmodeled content
  `type`s used to discard the source JSON and re-emit a hardcoded `{"type":"unknown"}`, dropping data
  a forward-compat peer received intact. It now retains the source `IJsonObject` and re-emits it
  verbatim. *Test:* added a `ContentTests` case round-tripping a content block with an unmodeled
  `type` (preserving the type plus extra scalar/nested fields).

*Not gaps (verified): marker capabilities (`CompletionCapabilityModel`, `LoggingCapabilityModel`)
that serialize as `{}`, `NullLogger` no-ops, delegating/`: base(...)` constructors, parameterless
object-initializer constructors, and `catch (OperationCanceledException)` blocks are all correct
as-is.*

## References

- Spec: <https://modelcontextprotocol.io/specification/2025-11-25>
- Changelog: <https://modelcontextprotocol.io/specification/2025-11-25/changelog>
</content>
</invoke>
