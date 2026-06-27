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

- [ ] Confirm newline-delimited UTF-8 framing with **no embedded newlines** in any emitted message.
- [ ] Confirm stdout carries *only* MCP messages; logging goes to stderr (now explicitly allowed for all levels).
- [ ] Guard + test that no JSON-RPC batching is emitted/required (removed in 2025-06-18).

**Exit:** passes a stdio round-trip conformance test against the negotiated version.

---

## Phase C — Modern tools

The highest-value feature jump.

- [ ] **`Tool` model** (`Protocol/Models/Tool.cs`): add `Title`, `OutputSchema` (`ObjectSchema`),
  `Annotations` (new `ToolAnnotations`: `title`, `readOnlyHint`, `destructiveHint`, `idempotentHint`,
  `openWorldHint`), `Icons`, `_meta`.
- [ ] **Structured output** (`CallToolResult.cs`): add `StructuredContent` (arbitrary JSON object).
  When a tool declares `outputSchema`, the result includes `structuredContent` **and** a
  serialized-JSON text block for back-compat.
- [ ] **New content types** in `Content.Create` (`Protocol/Models/Content.cs`): `AudioContent`
  (`type:"audio"`) and **resource links** (`type:"resource_link"`).
- [ ] **Validation errors → tool errors.** When tool args fail schema validation, return
  `CallToolResult { isError: true }` instead of JSON-RPC `InvalidParams`
  (`HandleCallToolRequest` / `DefaultToolsController`) so the model can self-correct (SEP-1303).
- [ ] **Icons** shared model (`src` / `sizes` / `mimeType`), reused by resources/prompts later.
- [ ] **JSON Schema 2020-12** as default dialect across the `*Schema.cs` models — emit `$schema` where appropriate.

**Exit:** structured-output round-trip test; annotations + icons appear in `tools/list`.

---

## Phase D — Pagination

- [ ] Add opaque `cursor` param to list requests and `nextCursor` to list results across
  `tools/list`, `resources/list`, `resources/templates/list`, `prompts/list`. Today `tools/list`
  sends an empty params block (`McpClient.cs:130`) and `ListToolsResult` has no cursor field. Update
  `IToolsController`, `IResourcesController`, `IPromptController` to accept/return cursors.

**Exit:** multi-page list round-trips.

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
