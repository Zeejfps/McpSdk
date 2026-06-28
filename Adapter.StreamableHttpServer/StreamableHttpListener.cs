using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Server;
using McpSdk.Shared;

namespace McpSdk.Adapter.StreamableHttpServer
{
    /// <summary>
    /// A zero-dependency <see cref="HttpListener"/>-based MCP server for the Streamable HTTP transport
    /// (2025-11-25): a single endpoint that accepts JSON-RPC over <c>POST</c>, issues an
    /// <c>Mcp-Session-Id</c> on initialize, requires the <c>MCP-Protocol-Version</c> header thereafter,
    /// and rejects disallowed <c>Origin</c>s with <c>403</c> (DNS-rebinding guard).
    ///
    /// Each session is an <see cref="HttpServerChannel"/> wrapped in a shared <see cref="JsonRpcPeer"/>;
    /// the <c>onSession</c> callback builds and starts an <c>McpServer</c> over that peer (mirroring the
    /// old SSE adapter's per-connection callback) and is awaited before the first frame is dispatched, so
    /// the server is subscribed in time to answer <c>initialize</c>. <c>GET</c> opens the server→client
    /// SSE stream (with <c>Last-Event-ID</c> resumption); <c>DELETE</c> terminates the session.
    /// </summary>
    public sealed class StreamableHttpListener
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private const string SessionIdHeader = "Mcp-Session-Id";
        private const string ProtocolVersionHeader = "MCP-Protocol-Version";

        private readonly HttpListener _listener = new HttpListener();
        private readonly string _baseUrl;
        private readonly string _endpointPath;
        private readonly IJson _json;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly Func<ITransport, Task> _onSession;
        private readonly HashSet<string> _allowedOrigins;
        private readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();
        private readonly object _sessionsGate = new object();

        private CancellationTokenSource _cts;
        private Task _listenTask;

        /// <param name="baseUrl">Origin to bind, e.g. <c>http://localhost:3000</c>.</param>
        /// <param name="endpointPath">The single MCP endpoint path, e.g. <c>/mcp</c>.</param>
        /// <param name="onSession">Builds and starts an McpServer over the new session's transport.</param>
        /// <param name="allowedOrigins">Permitted <c>Origin</c> values; <c>null</c> disables the check
        /// (no browser-rebinding protection — appropriate only for non-browser deployments).</param>
        public StreamableHttpListener(
            string baseUrl,
            string endpointPath,
            IJson json,
            ILoggerFactory loggerFactory,
            Func<ITransport, Task> onSession,
            IEnumerable<string> allowedOrigins = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _endpointPath = endpointPath;
            _json = json;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.Create<StreamableHttpListener>();
            _onSession = onSession ?? throw new ArgumentNullException(nameof(onSession));
            _allowedOrigins = allowedOrigins == null ? null : new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);
        }

        public Task Start()
        {
            _listener.Prefixes.Add(_baseUrl + "/");
            _listener.Start();
            _cts = new CancellationTokenSource();
            _listenTask = Listen(_cts.Token);
            _logger.LogDebug($"Streamable HTTP listening on {_baseUrl}{_endpointPath}");
            return Task.CompletedTask;
        }

        public async Task Stop()
        {
            _cts?.Cancel();
            try
            {
                _listener.Stop();
                if (_listenTask != null)
                    await _listenTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
            finally
            {
                _cts?.Dispose();
            }
        }

        private async Task Listen(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break; // listener stopped
                }

                // Handle each request without blocking the accept loop.
                _ = HandleContext(context);
            }
        }

        private async Task HandleContext(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            try
            {
                if (!string.Equals(request.Url.AbsolutePath, _endpointPath, StringComparison.OrdinalIgnoreCase))
                {
                    WriteStatus(response, 404);
                    return;
                }

                // DNS-rebinding guard: reject a disallowed Origin before doing anything else.
                if (!IsOriginAllowed(request))
                {
                    _logger.LogDebug($"Rejected disallowed Origin: {request.Headers["Origin"]}");
                    WriteStatus(response, 403);
                    return;
                }

                switch (request.HttpMethod.ToUpperInvariant())
                {
                    case "POST":
                        await HandlePost(request, response).ConfigureAwait(false);
                        break;
                    case "GET":
                        await HandleGet(request, response).ConfigureAwait(false);
                        break;
                    case "DELETE":
                        await HandleDelete(request, response).ConfigureAwait(false);
                        break;
                    default:
                        WriteStatus(response, 405);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                try { WriteStatus(response, 500); } catch { /* connection already gone */ }
            }
        }

        private async Task HandlePost(HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Utf8NoBom))
                body = await reader.ReadToEndAsync().ConfigureAwait(false);

            var sessionId = request.Headers[SessionIdHeader];
            HttpServerChannel channel;

            if (string.IsNullOrEmpty(sessionId))
            {
                // No session yet: this is the initialize request. Create the session channel + peer, let
                // the consumer build & start its McpServer (subscribing before we dispatch), echo the id.
                channel = new HttpServerChannel(_json, _loggerFactory, Guid.NewGuid().ToString("N"));
                var peer = new JsonRpcPeer(channel, _loggerFactory);
                lock (_sessionsGate)
                    _sessions[channel.SessionId] = new Session(channel, peer);

                await _onSession(peer).ConfigureAwait(false);
                response.Headers[SessionIdHeader] = channel.SessionId;
            }
            else
            {
                // Post-initialize: the negotiated protocol version is a required header.
                if (string.IsNullOrEmpty(request.Headers[ProtocolVersionHeader]))
                {
                    WriteStatus(response, 400);
                    return;
                }

                Session session;
                lock (_sessionsGate)
                    _sessions.TryGetValue(sessionId, out session);

                if (session == null)
                {
                    // Unknown or expired session: the client must reinitialize.
                    WriteStatus(response, 404);
                    return;
                }
                channel = session.Channel;
            }

            var responseBody = await channel.HandleInboundPost(body).ConfigureAwait(false);
            if (responseBody == null)
            {
                WriteStatus(response, 202); // notification/response acknowledged, no body
                return;
            }

            var bytes = Utf8NoBom.GetBytes(responseBody);
            response.StatusCode = 200;
            response.ContentType = "application/json";
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            response.Close();
        }

        private async Task HandleGet(HttpListenerRequest request, HttpListenerResponse response)
        {
            var sessionId = request.Headers[SessionIdHeader];
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(request.Headers[ProtocolVersionHeader]))
            {
                WriteStatus(response, 400);
                return;
            }

            Session session;
            lock (_sessionsGate)
                _sessions.TryGetValue(sessionId, out session);

            if (session == null)
            {
                WriteStatus(response, 404);
                return;
            }

            var channel = session.Channel;

            response.StatusCode = 200;
            response.ContentType = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.SendChunked = true;

            var output = response.OutputStream;
            var writeLock = new SemaphoreSlim(1, 1);

            async Task WriteRaw(string text)
            {
                var bytes = Utf8NoBom.GetBytes(text);
                await writeLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    await output.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    await output.FlushAsync().ConfigureAwait(false);
                }
                finally
                {
                    writeLock.Release();
                }
            }

            // The channel pushes server→client frames through here as SSE `id:`/`data:` events,
            // resuming after the client's Last-Event-ID when present.
            var lastEventId = request.Headers["Last-Event-ID"];
            using var handle = channel.AttachStream(lastEventId, (eventId, json) => WriteRaw($"id: {eventId}\ndata: {json}\n\n"));

            // Close the stream when the listener stops or this session is terminated (DELETE). Captured
            // up front so we never touch _cts after Stop disposes it.
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, channel.Lifetime);
            var cancellationToken = linkedCts.Token;
            try
            {
                // Hold the stream open, sending SSE comment heartbeats so a dropped client surfaces as a
                // write failure. Unblocks when the listener stops.
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
                    await WriteRaw(": ping\n\n").ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"SSE stream ended: {ex.Message}");
            }
            finally
            {
                try { response.Close(); } catch { /* connection already torn down */ }
            }
        }

        private async Task HandleDelete(HttpListenerRequest request, HttpListenerResponse response)
        {
            var sessionId = request.Headers[SessionIdHeader];
            Session session = null;
            if (!string.IsNullOrEmpty(sessionId))
            {
                lock (_sessionsGate)
                {
                    if (_sessions.TryGetValue(sessionId, out session))
                        _sessions.Remove(sessionId);
                }
            }

            if (session == null)
            {
                WriteStatus(response, 404);
                return;
            }

            // Stopping the peer cancels in-flight requests and stops the channel, which releases any open
            // POSTs and signals the GET loop to close.
            await session.Peer.Stop().ConfigureAwait(false);
            WriteStatus(response, 200);
        }

        private bool IsOriginAllowed(HttpListenerRequest request)
        {
            var origin = request.Headers["Origin"];
            if (origin == null)
                return true; // non-browser client; there is no Origin to forge
            if (_allowedOrigins == null)
                return true; // origin checking disabled
            return _allowedOrigins.Contains(origin);
        }

        private static void WriteStatus(HttpListenerResponse response, int statusCode)
        {
            response.StatusCode = statusCode;
            response.Close();
        }

        // One MCP session: the HTTP/SSE delivery channel and the JSON-RPC peer the McpServer runs over.
        private sealed class Session
        {
            public Session(HttpServerChannel channel, JsonRpcPeer peer)
            {
                Channel = channel;
                Peer = peer;
            }

            public HttpServerChannel Channel { get; }
            public JsonRpcPeer Peer { get; }
        }
    }
}
