using McpSdk.Client;
using McpSdk.Shared;

namespace StdioToSseBridge;

public sealed class Bridge
{
    private readonly ISseClient _sseClient;
    private readonly ILogger _logger;
    private Task _readStdInTask;
    private string _url;
    private TaskCompletionSource<bool> _startedSrc;

    public Bridge(ISseClient sseClient, ILoggerFactory loggerFactory)
    {
        _sseClient = sseClient;
        _logger = loggerFactory.Create<Bridge>();
        _url = "http://localhost:3000/messages";
    }

    public async Task Run()
    {
        _startedSrc = new TaskCompletionSource<bool>();
        _sseClient.EventReceived += OnSseEventReceived;
        await _sseClient.Connect("http://localhost:3000/sse");
        await _startedSrc.Task;
        _logger.LogDebug("Bridge Connected");
        await ReadStdIn();
    }

    private async Task ReadStdIn()
    {
        string? line;
        while ((line = await Console.In.ReadLineAsync().ConfigureAwait(false)) != null)
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
            _url = $"http://localhost:3000{sseEvent.Data}";
            _logger.LogDebug($"Message URL: {_url}");
            _startedSrc.SetResult(true);
        }
        else if (sseEvent.Kind == "message")
        {
            var message = sseEvent.Data;
            Console.Out.WriteLine(message);
        }
    }
}