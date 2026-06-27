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

- [ ] **Elicitation** (new client capability). Server→client `elicitation/create` (carries `message`
  + a restricted flat primitive `requestedSchema`); client replies `accept` / `decline` / `cancel`
  with content. Mirror the existing roots/sampling wiring: new `ElicitationCapabilityModel`,
  `IElicitationController`, `ElicitRequest` / `ElicitResult`, and a branch in
  `McpClient.OnRequestReceived` (`McpClient.cs:34`). Include the 2025-11-25 `EnumSchema`
  (titled/untitled, single/multi-select), URL-mode elicitation, and primitive default values.
- [ ] **Sampling with tools.** `CreateMessageRequest` gains optional `tools` + `toolChoice`;
  `SamplingMessage` / `CreateMessagesResult` gain tool-call/tool-result content
  (`ISamplingController` + models).

**Exit:** elicitation accept/decline/cancel round-trip; a sampling request advertising tools round-trips.

---

## Phase F — Resources / prompts / completion polish

- [ ] Add `icons` + `title` + `_meta` to `Resource`, `ResourceTemplate`, `Prompt`, `PromptMessage`.
- [ ] Add completion `context` (previously-resolved variables) to `CompletionRequest` (2025-06-18).
- [ ] **Bug:** `ResourcesCapabilityModel` wiring at `McpServer.cs:59` copies `listChanged` into the
  `subscribe`/resource-changed flag; these are independent capabilities.

---

## Phase G — Streamable HTTP transport *(deferred follow-up)*

Replaces the legacy dual-endpoint HTTP+SSE (`Adapter.SseServer/HttpListenerSseServer.cs`).

- [ ] Single MCP endpoint: `POST` returns `application/json` **or** `text/event-stream`; optional
  `GET` opens a server→client SSE stream.
- [ ] `Mcp-Session-Id` issued on initialize and echoed thereafter; `MCP-Protocol-Version` header
  required on subsequent HTTP requests.
- [ ] `Origin` validation → **HTTP 403**; resumability via `Last-Event-ID` + per-stream event IDs;
  SSE polling / server-initiated disconnect (SEP-1699).
- [ ] New `StreamableHttpClient`; retire or keep the old SSE adapter behind a flag.

---

## Cross-cutting

- **Tests:** extend `Server.Tests` / `Client.Tests` with a conformance case per phase (negotiation,
  pagination, structured output, elicitation).
- **Docs:** update `README.md` examples; add a capability/feature matrix.
- **Sequencing:** A → B → C → D → E → F can each merge independently; G last.

## References

- Spec: <https://modelcontextprotocol.io/specification/2025-11-25>
- Changelog: <https://modelcontextprotocol.io/specification/2025-11-25/changelog>
</content>
</invoke>
