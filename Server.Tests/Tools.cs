using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests;

public delegate Task<CallToolResult> CallToolFunc(IJsonObject args);

public class Tools : IToolsCapability
{
    private readonly IJson _json;
    private readonly Dictionary<string, Tool> _toolByNameLookup = new();
    private readonly Dictionary<string, CallToolFunc> _funcByToolNameLookup = new();
    
    public Tools(IJson json)
    {
        _json = json;
    }

    public void AddTool(Tool tool, CallToolFunc callToolFunc)
    {
        _toolByNameLookup.Add(tool.Name, tool);
        _funcByToolNameLookup.Add(tool.Name, callToolFunc);
    }

    public bool IsListChangedNotificationSupported => false;

    public Task<ListToolsResult> ListTools()
    {
        var result = new ListToolsResult(_json, _toolByNameLookup.Values.ToArray());
        return Task.FromResult(result);
    }

    public async Task<CallToolResult> CallTool(CallToolArguments arguments)
    {
        var toolName = arguments.ToolName;
        if (!_toolByNameLookup.TryGetValue(toolName, out var tool))
        {
            var content = new TextContent(_json, $"No tool found with name: {toolName}");
            return new CallToolResult(_json, new Content[] { content }, true);
        }

        var inputSchema = tool.InputSchema;
        if (!arguments.JsonObject.IsValid(inputSchema, out var errors))
        {
            var content = new Content[errors.Count];
            for (var i = 0; i < errors.Count; i++)
            {
                content[i] = new TextContent(_json, errors[i]);
            }
            return new CallToolResult(_json, content, true);
        }

        var toolArguments = arguments.ToolArguments;
        var callToolFunc = _funcByToolNameLookup[toolName];
        return await callToolFunc(toolArguments);
    }
}