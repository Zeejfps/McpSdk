using McpSdk.Client;
using McpSdk.Shared;

namespace StdioToSseBridge;

public sealed class Bridge
{
    private readonly ISseClient _sseClient;
    private readonly ILogger _logger;
    
    private string _url;

    public Bridge(ISseClient sseClient, ILoggerFactory loggerFactory)
    {
        _sseClient = sseClient;
        _logger = loggerFactory.Create<Bridge>();
        _url = "http://localhost:3000/messages";
    }

    public async Task Run()
    {
        _sseClient.EventReceived += OnSseEventReceived;
        await _sseClient.Connect("http://localhost:3000");
        _logger.LogDebug("Bridge Connected");
        await ReadStdIn().ConfigureAwait(false);
    }

    private async Task ReadStdIn()
    {
        string? line;
        while ((line = await Console.In.ReadLineAsync()) != null)
        {
            _logger.LogDebug($"Sending: {line} to {_url}");
            await _sseClient.SendMessage(_url, line);
        }
    }

    private void OnSseEventReceived(ISseEvent sseEvent)
    {
        _logger.LogDebug($"Received: {sseEvent}");
        if (sseEvent.Kind == "endpoint")
        {
            _url = $"http://localhost:3000/{sseEvent.Data}";
        }
        else if (sseEvent.Kind == "message")
        {
            var message = sseEvent.Data;
            Console.Out.WriteLine(message);
        }
    }
}