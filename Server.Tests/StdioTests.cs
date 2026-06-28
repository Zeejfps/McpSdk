using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server.Tests;

public sealed class StdioTests
{

    public async Task Run()
    {
        var json = new NewtonsoftJson();
        var mcpServer = new ServerBuilder()
            .WithName("Demo Server")
            .WithVersion("1.0.0")
            .WithConsoleLogger()
            .ConfigureContext(c => c
                .AddSingleton<IJson>(json)
                .AddStdioTransport())
            .WithDefaultToolsCapability(json, tools =>
            {
                tools.AddTool(new TestToolHandler());
            })
            .Build();

        await mcpServer.Start();
        while (true)
        {
            await Task.Delay(1000);
        }
    }
    
}