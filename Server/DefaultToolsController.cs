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
            // Offset-cursor paging shared with CompositeToolsController: unrecognized cursor -> first page,
            // offsets clamped, a non-positive PageSize means "no paging" (one page, no nextCursor).
            var result = PaginationCursor.GetPage(_toolsInOrder, request?.Cursor, PageSize, h => h.Tool);
            return Task.FromResult(result);
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