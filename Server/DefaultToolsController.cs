using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public sealed class DefaultToolsController : IToolsController
    {
        private readonly IJson _json;
        private readonly Dictionary<string, ITool> _toolByNameLookup = new();

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

        public void AddTool(ITool tool)
        {
            _toolByNameLookup.Add(tool.Info.Name, tool);
            ListChanged?.Invoke();
        }

        public Task<ListToolsResult> ListTools(ListToolsRequest request)
        {
            // Snapshot a stable ordering so an offset cursor refers to the same slice across calls.
            var allTools = _toolByNameLookup.Values.Select(tool => tool.Info).ToArray();

            // Recover where this page starts. An unrecognized/malformed cursor falls back to the
            // first page rather than erroring; offsets are clamped into range so a stale cursor
            // (e.g. tools removed since it was issued) yields an empty final page, never a throw.
            var offset = 0;
            if (request?.Cursor != null && PaginationCursor.TryDecodeOffset(request.Cursor, out var decoded))
                offset = decoded;
            if (offset < 0)
                offset = 0;
            if (offset > allTools.Length)
                offset = allTools.Length;

            var pageSize = PageSize is > 0 ? PageSize.Value : allTools.Length;

            var page = allTools.Skip(offset).Take(pageSize).ToArray();

            var nextOffset = offset + page.Length;
            var nextCursor = nextOffset < allTools.Length
                ? PaginationCursor.EncodeOffset(nextOffset)
                : null;

            return Task.FromResult(new ListToolsResult(page, nextCursor));
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request)
        {
            var toolName = request.ToolName;
            if (!_toolByNameLookup.TryGetValue(toolName, out var tool))
            {
                var content = new TextContent($"No tool found with name: {toolName}");
                return new CallToolResult([content], true);
            }

            // Treat omitted arguments as an empty object so validation (rather than a null-ref) decides
            // whether required inputs are missing.
            var toolArguments = request.ToolArguments ?? _json.Object(_ => { });

            // SEP-1303: schema-validation failures are returned to the model as a tool error
            // (isError: true) so it can self-correct, not raised as a JSON-RPC protocol error.
            var inputSchema = tool.Info.InputSchema.AsJsonObject(_json);
            if (!toolArguments.IsValid(inputSchema, out var errors))
            {
                var content = new Content[errors.Count];
                for (var i = 0; i < errors.Count; i++)
                {
                    content[i] = new TextContent(errors[i]);
                }
                return new CallToolResult(content, true);
            }

            return await tool.Call(toolArguments);
        }
    }

    public static class DefaultToolsControllerExtensions
    {
        public static ServerBuilder WithDefaultToolsCapability(this ServerBuilder builder, IJson json, Action<DefaultToolsController> configure)
        {
            var toolsController = new DefaultToolsController(json);
            configure(toolsController);
            builder.WithToolsCapability(toolsController);
            return builder;
        }
    }
}