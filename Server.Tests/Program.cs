using McpSdk.Adapter.Newtonsoft.Json;
using McpSdk.Adapter.StreamableHttpServer;
using McpSdk.Server;
using McpSdk.Server.Tests;

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
    // New DI builder API: serializer + stderr-only console logger (stdout is reserved for protocol frames)
    // + the stdio transport + the test tool.
    var builder = new ServerBuilder("Stdio Conf Server", "1.0.0");
    builder.Context.AddNewtonsoftJson();
    builder.Context.AddConsoleLogger();
    builder.Context.AddStdioTransport();
    builder.Context.AddToolsCapability(tools => tools.AddTool(new TestToolHandler()));
    var stdioServer = builder.Build();

    await stdioServer.Start();
    await Task.Delay(Timeout.Infinite);
    return;
}

// Streamable HTTP demo server: a single endpoint serving the test tool, one McpServer per session.
if (args.Length > 0 && args[0] == "streamable-http-server")
{
    var baseUrl = args.Length > 1 ? args[1] : "http://localhost:3000";
    const string endpointPath = "/mcp";

    // New DI builder API: the Streamable HTTP host builds one McpServer per session in a child scope.
    // ConfigureSession contributes the test tool to each session (mirroring the original per-session
    // server built inside onSession); the stderr-only console logger backs the listener.
    var builder = new ServerBuilder("Streamable HTTP Demo Server", "1.0.0");
    builder.Context.AddNewtonsoftJson();
    builder.Context.AddConsoleLogger();
    builder.Context.AddStreamableHttpTransport(baseUrl, endpointPath, http => http.ConfigureSession(session =>
        session.Context.AddToolsCapability(tools => tools.AddTool(new TestToolHandler()))));
    var host = builder.Build();

    await host.Start();
    Console.Error.WriteLine($"Streamable HTTP server listening on {baseUrl}{endpointPath}");
    await Task.Delay(Timeout.Infinite);
    return;
}

Console.Error.WriteLine("usage: McpSdk.Server.Tests <conformance|stdio-server|streamable-http-server [baseUrl]>");
Environment.Exit(1);