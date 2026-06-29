using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public sealed class DefaultToolsController : IToolsController
    {
        private readonly IJson _json;
        private readonly Dictionary<string, IToolHandler> _toolByNameLookup = new();

        // Parallel to the name lookup: preserves registration order so an offset-based pagination
        // cursor refers to the same slice across calls. Dictionary enumeration order is not a
        // documented guarantee, so we track order explicitly here. Both are mutated together.
        private readonly List<IToolHandler> _toolsInOrder = new();

        public event Action ListChanged;
        public bool IsListChangedNotificationSupported => false;

        /// <summary>
        /// Maximum number of tools returned per <c>tools/list</c> page. When null (the default) every
        /// tool is returned in a single page with no <c>nextCursor</c>. Set a positive value to page;
        /// any value &lt;= 0 is treated as "no paging".
        /// </summary>
        public int? PageSize { get; set; }

        public DefaultToolsController(IJson json)
        {
            _json = json;
        }

        public void AddTool(IToolHandler toolHandler)
        {
            // Add to the lookup first: it throws on a duplicate name, keeping the ordered list in sync.
            _toolByNameLookup.Add(toolHandler.Tool.Name, toolHandler);
            _toolsInOrder.Add(toolHandler);
            ListChanged?.Invoke();
        }

        public bool RemoveTool(string name)
        {
            if (!_toolByNameLookup.TryGetValue(name, out var tool))
                return false;

            _toolByNameLookup.Remove(name);
            _toolsInOrder.Remove(tool);
            ListChanged?.Invoke();
            return true;
        }

        public Task<ListToolsResult> ListTools(ListToolsRequest request, McpRequestContext context)
        {
            var toolCount = _toolsInOrder.Count;

            // Recover where this page starts. An unrecognized/malformed cursor falls back to the
            // first page rather than erroring; offsets are clamped into range so a stale cursor
            // (e.g. tools removed since it was issued) yields an empty final page, never a throw.
            var offset = 0;
            if (request?.Cursor != null && PaginationCursor.TryDecodeOffset(request.Cursor, out var decoded))
                offset = decoded;
            if (offset < 0)
                offset = 0;
            if (offset > toolCount)
                offset = toolCount;

            var pageSize = PageSize is > 0 ? PageSize.Value : toolCount;
            var take = Math.Min(pageSize, toolCount - offset);

            var page = new Tool[take];
            for (var i = 0; i < take; i++)
                page[i] = _toolsInOrder[offset + i].Tool;

            var nextOffset = offset + page.Length;
            var nextCursor = nextOffset < toolCount
                ? PaginationCursor.EncodeOffset(nextOffset)
                : null;

            return Task.FromResult(new ListToolsResult(page, nextCursor));
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request, McpRequestContext context)
        {
            var toolName = request.ToolName;
            if (!_toolByNameLookup.TryGetValue(toolName, out var toolHandler))
            {
                return CallToolResult.Error($"No tool found with name: {toolName}");
            }

            // Treat omitted arguments as an empty object so validation (rather than a null-ref) decides
            // whether required inputs are missing.
            var toolArguments = request.ToolArguments ?? _json.Object(_ => { });

            // SEP-1303: schema-validation failures are returned to the model as a tool error
            // (isError: true) so it can self-correct, not raised as a JSON-RPC protocol error.
            var inputSchema = toolHandler.Tool.InputSchema.AsJsonObject(_json);
            if (!toolArguments.IsValid(inputSchema, out var errors))
            {
                var content = new Content[errors.Count];
                for (var i = 0; i < errors.Count; i++)
                {
                    content[i] = new TextContent(errors[i]);
                }
                return CallToolResult.Error(content);
            }

            return await toolHandler.Call(toolArguments, context);
        }
    }
}