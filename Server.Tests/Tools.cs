using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests;

public class Tools : IToolsCapability
{
    private readonly IJson _json;
    private readonly Dictionary<string, Tool> _toolByNameLookup = new();
    
    public Tools(IJson json)
    {
        _json = json;
    }

    public void AddTool(Tool tool, Func<IJsonObject, CallToolResult> callToolFunc)
    {
        _toolByNameLookup.Add(tool.Name, tool);
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
        return new CallToolResult(_json, null, false);
    }
}