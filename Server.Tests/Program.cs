using McpSdk.Adapter.ConsoleLogger;
using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Server;
using McpSdk.Server.Tests;
using McpSdk.Server.Tests.Conformance;

if (args.Length > 0 && args[0] == "conformance")
{
    var failures = await ConformanceTests.RunAll();
    Environment.Exit(failures);
    return;
}

// Child-process entry point for the stdio round-trip conformance test. Speaks MCP over stdio and
// blocks forever; the transport reserves stdout for protocol frames and sends all logging to stderr.
if (args.Length > 0 && args[0] == "stdio-server")
{
    var stdioJson = new NewtonsoftJson();
    var stdioServer = new ServerBuilder()
        .WithName("Stdio Conf Server")
        .WithVersion("1.0.0")
        .WithConsoleLogger()
        .WithStdioTransport(stdioJson)
        .WithDefaultToolsCapability(stdioJson, tools => tools.AddTool(new TestToolHandler()))
        .Build();

    await stdioServer.Start();
    await Task.Delay(Timeout.Infinite);
    return;
}

// Streamable HTTP demo server: a single endpoint serving the test tool, one McpServer per session.
if (args.Length > 0 && args[0] == "streamable-http-server")
{
    var httpJson = new NewtonsoftJson();
    var httpLoggerFactory = new ServerConsoleLoggerFactory();
    var baseUrl = args.Length > 1 ? args[1] : "http://localhost:3000";
    const string endpointPath = "/mcp";

    var listener = new StreamableHttpListener(
        baseUrl,
        endpointPath,
        httpJson,
        httpLoggerFactory,
        onSession: async transport =>
        {
            var server = new ServerBuilder()
                .WithName("Streamable HTTP Demo Server")
                .WithVersion("1.0.0")
                .WithLogger(httpLoggerFactory)
                .WithStreamableHttpTransport(transport)
                .WithDefaultToolsCapability(httpJson, tools => tools.AddTool(new TestToolHandler()))
                .Build();
            await server.Start();
        });

    await listener.Start();
    Console.Error.WriteLine($"Streamable HTTP server listening on {baseUrl}{endpointPath}");
    await Task.Delay(Timeout.Infinite);
    return;
}

Console.Error.WriteLine("usage: McpSdk.Server.Tests <conformance|stdio-server|streamable-http-server [baseUrl]>");
Environment.Exit(1);