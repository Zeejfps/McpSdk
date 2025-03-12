using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server
{
    public delegate void WriteToolFunc(Tool.Writer toolWriter);
    public delegate Task<CallToolResult> CallToolFunc(IJsonObject args);

    public class DefaultToolsController : IToolsController
    {
        private readonly IJson _json;
        private readonly Dictionary<string, Tool> _toolByNameLookup = new();
        private readonly Dictionary<string, CallToolFunc> _funcByToolNameLookup = new();
    
        public DefaultToolsController(IJson json)
        {
            _json = json;
        }
        
        public void AddTool(WriteToolFunc writeTool, CallToolFunc callToolFunc)
        {
            var toolAsString = _json.Stringify(jsonWriter =>
            {
                writeTool(Tool.CreateWriter(jsonWriter));
            });
            var tool = new Tool(_json.Parse(toolAsString));
            AddTool(tool, callToolFunc);
        }

        public void AddTool(Tool tool, CallToolFunc callToolFunc)
        {
            _toolByNameLookup.Add(tool.Name, tool);
            _funcByToolNameLookup.Add(tool.Name, callToolFunc);
            ListChanged?.Invoke();
        }

        public event Action ListChanged;
        public bool IsListChangedNotificationSupported => false;

        public Task<ListToolsResult> ListTools()
        {
            var tools = _toolByNameLookup.Values.ToArray();
            foreach (var tool in tools)
            {
                Console.Error.WriteLine($"Tool: {tool}");
            }
            var result = new ListToolsResult(_json, tools);
            return Task.FromResult(result);
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request)
        {
            var toolName = request.ToolName;
            if (!_toolByNameLookup.TryGetValue(toolName, out var tool))
            {
                var content = new TextContent(_json, $"No tool found with name: {toolName}");
                return new CallToolResult(_json, new Content[] { content }, true);
            }

            var inputSchema = tool.InputSchema;
            if (!request.JsonObject.IsValid(inputSchema, out var errors))
            {
                var content = new Content[errors.Count];
                for (var i = 0; i < errors.Count; i++)
                {
                    content[i] = new TextContent(_json, errors[i]);
                }
                return new CallToolResult(_json, content, true);
            }

            var toolArguments = request.ToolArguments;
            var callToolFunc = _funcByToolNameLookup[toolName];
            return await callToolFunc(toolArguments);
        }
    }
}