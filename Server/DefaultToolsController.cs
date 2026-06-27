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
        
        public DefaultToolsController(IJson json)
        {
            _json = json;
        }

        public void AddTool(ITool tool)
        {
            _toolByNameLookup.Add(tool.Info.Name, tool);
            ListChanged?.Invoke();
        }
        
        public Task<ListToolsResult> ListTools()
        {
            var tools = _toolByNameLookup.Values.Select(tool => tool.Info).ToArray();
            var result = new ListToolsResult(tools);
            return Task.FromResult(result);
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