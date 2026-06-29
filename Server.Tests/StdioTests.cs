using McpSdk.Adapter.Newtonsoft.Json;

namespace McpSdk.Server.Tests;

public sealed class StdioTests
{

    public async Task Run()
    {
        var builder = new ServerBuilder("Demo Server", "1.0.0");
        builder.Context.AddNewtonsoftJson();
        builder.Context.AddConsoleLogger();
        builder.Context.AddStdioTransport();
        builder.Context.AddToolsCapability(tools => tools.AddTool(new TestToolHandler()));
        var mcpServer = builder.Build();

        await mcpServer.Start();
        while (true)
        {
            await Task.Delay(1000);
        }
    }
    
}