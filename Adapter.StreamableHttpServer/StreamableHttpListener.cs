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
    /// Each session gets its own <see cref="StreamableHttpServerTransport"/>; the
    /// <c>onSession</c> callback builds and starts an <c>McpServer</c> over it (mirroring the old SSE
    /// adapter's per-connection session callback), and is awaited before the first frame is dispatched
    /// so the server is subscribed in time to answer <c>initialize</c>.
    ///
    /// The standalone <c>GET</c> SSE stream and <c>DELETE</c> session termination arrive in later Phase G
    /// increments; for now both are answered with <c>405</c>.
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
        private readonly Dictionary<string, StreamableHttpServerTransport> _sessions = new Dictionary<string, StreamableHttpServerTransport>();
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
                    case "GET":     // standalone server→client SSE stream: Phase G increment 2
                    case "DELETE":  // client-initiated session termination: Phase G increment 3
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
            StreamableHttpServerTransport transport;

            if (string.IsNullOrEmpty(sessionId))
            {
                // No session yet: this is the initialize request. Create the session + transport, let the
                // consumer build & start its McpServer (subscribing before we dispatch), then echo the id.
                transport = new StreamableHttpServerTransport(_json, _loggerFactory, Guid.NewGuid().ToString("N"));
                lock (_sessionsGate)
                    _sessions[transport.SessionId] = transport;

                await _onSession(transport).ConfigureAwait(false);
                response.Headers[SessionIdHeader] = transport.SessionId;
            }
            else
            {
                // Post-initialize: the negotiated protocol version is a required header.
                if (string.IsNullOrEmpty(request.Headers[ProtocolVersionHeader]))
                {
                    WriteStatus(response, 400);
                    return;
                }

                lock (_sessionsGate)
                    _sessions.TryGetValue(sessionId, out transport);

                if (transport == null)
                {
                    // Unknown or expired session: the client must reinitialize.
                    WriteStatus(response, 404);
                    return;
                }
            }

            var responseBody = await transport.Deliver(body).ConfigureAwait(false);
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
    }
}
