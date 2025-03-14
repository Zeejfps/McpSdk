using McpSdk.Client;
using McpSdk.Shared;

namespace StdioToSseBridge;

public sealed class Bridge
{
    private readonly string _host;
    private readonly int _port;
    private readonly ISseClient _sseClient;
    private readonly ILogger _logger;
    private Task _readStdInTask;
    private string _url;
    private CancellationTokenSource _cts;
    private TaskCompletionSource<bool> _startedSrc;

    public Bridge(string host, int port, ISseClient sseClient, ILoggerFactory loggerFactory)
    {
        _host = host;
        _port = port;
        _sseClient = sseClient;
        _logger = loggerFactory.Create<Bridge>();
        _url = $"http://{host}:{port}/messages";
    }

    public async Task Run()
    {
        _cts = new CancellationTokenSource();
        var cancellationToken = _cts.Token;
        _startedSrc = new TaskCompletionSource<bool>();
        _sseClient.EventReceived += OnSseEventReceived;
        _sseClient.Disconnected += OnSseClientDisconnected;
        await _sseClient.Connect($"http://{_host}:{_port}/sse", cancellationToken);
        await _startedSrc.Task;
        _logger.LogDebug("Bridge Connected");
        await ReadStdIn(cancellationToken);
        _logger.LogDebug("Bridge Disconnected");
    }

    private void OnSseClientDisconnected()
    {
        _startedSrc.TrySetCanceled();
        _cts.Cancel();
        Environment.Exit(0);
    }

    private async Task ReadStdIn(CancellationToken cancellationToken = default)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (line == null)
                    break;
                
                _logger.LogDebug($"Sending: {line} to {_url}");
                await _sseClient.SendMessage(_url, line, cancellationToken);
            }
            _logger.LogDebug("Canceled");
        }
        catch (OperationCanceledException)
        {
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