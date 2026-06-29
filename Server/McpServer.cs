using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Protocol.Models.ServerCapabilities;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal delegate Task RequestHandler(RequestId requestId, IJsonObject arguments, McpRequestContext context);
    
    internal sealed class McpServer : IServer
    {
        private readonly ITransport _transport;
        private readonly ServerInfo _serverInfo;
        private readonly IToolsController _toolsController;
        private readonly IPromptController _promptController;
        private readonly IResourcesController _resourcesController;
        private readonly ICompletionController _completionController;
        private readonly ILogger _logger;
        private readonly Dictionary<string, RequestHandler> _requestHandlersByPathLookup = new();
        private readonly ServerCapabilitiesModel _capabilities = new();
        private readonly ConcurrentDictionary<RequestId, CancellationTokenSource> _inFlightRequests = new();
        private readonly ConcurrentDictionary<Task, byte> _inFlightRequestHandlers = new();

        private readonly bool _loggingEnabled;

        private bool _isRunning;

        private volatile bool _initialized;

        // The minimum severity the client asked for via logging/setLevel; null until set, in which case
        // the server emits every log (the spec leaves the pre-set behaviour to the server).
        private LoggingLevel? _minLogLevel;

        public McpServer(
            ITransport transport,
            ServerInfo serverInfo,
            ILoggerFactory loggerFactory,
            IToolsController toolsController,
            IPromptController promptController,
            IResourcesController resourcesController,
            ICompletionController completionController = null,
            bool loggingEnabled = false)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serverInfo = serverInfo ?? throw new ArgumentNullException(nameof(serverInfo));
            _logger = loggerFactory.Create<McpServer>();

            _toolsController = toolsController;
            _promptController = promptController;
            _resourcesController = resourcesController;
            _completionController = completionController;
            _loggingEnabled = loggingEnabled;

            _requestHandlersByPathLookup.Add("initialize", HandleInitializeRequest);

            // ping is a base-protocol utility: either party MAY send it and the receiver MUST reply
            // promptly with an empty result. Always available, independent of any capability.
            _requestHandlersByPathLookup.Add("ping", HandlePingRequest);

            if (_promptController != null)
            {
                _capabilities.Prompts = new PromptsCapabilityModel(_promptController.IsListChangedNotificationSupported);
                _requestHandlersByPathLookup.Add("prompts/list", HandleListPromptsRequest);
                _requestHandlersByPathLookup.Add("prompts/get", HandleGetPromptRequest);
                // The ListChanged subscription is wired in RegisterListeners (on Start) and removed in
                // UnregisterListeners (on Stop), alongside tools and resources — so the server only emits
                // notifications while it is running, and never leaks the handler after Stop.
            }
            
            if (_resourcesController != null)
            {
                _capabilities.Resources = new ResourcesCapabilityModel
                {
                    IsListChangedNotificationSupported = _resourcesController.IsListChangedNotificationSupported,
                    IsResourceChangedNotificationSupported = _resourcesController.IsResourceChangedNotificationSupported,
                };
                _requestHandlersByPathLookup.Add("resources/list", HandleListResourcesRequest);
                _requestHandlersByPathLookup.Add("resources/read", HandleReadResourceRequest);
                _requestHandlersByPathLookup.Add("resources/templates/list", HandleListResourceTemplatesRequest);

                // Only accept subscribe/unsubscribe when we advertise the subscribe capability —
                // advertise ⇒ serve, and never claim to serve what we don't advertise.
                if (_resourcesController.IsResourceChangedNotificationSupported == true)
                {
                    _requestHandlersByPathLookup.Add("resources/subscribe", HandleSubscribeRequest);
                    _requestHandlersByPathLookup.Add("resources/unsubscribe", HandleUnsubscribeRequest);
                }
            }
            
            if (_toolsController != null)
            {
                _capabilities.Tools = new ToolsCapabilityModel(_toolsController.IsListChangedNotificationSupported);
                _requestHandlersByPathLookup.Add("tools/list", HandleListToolsRequest);
                _requestHandlersByPathLookup.Add("tools/call", HandleCallToolRequest);
            }

            if (_completionController != null)
            {
                _capabilities.Completion = new CompletionCapabilityModel();
                _requestHandlersByPathLookup.Add("completion/complete", HandleCompleteRequest);
            }

            if (_loggingEnabled)
            {
                _capabilities.Logging = new LoggingCapabilityModel();
                _requestHandlersByPathLookup.Add("logging/setLevel", HandleSetLevelRequest);
            }
        }

        /// <summary>
        /// Emits a <c>notifications/message</c> log to the client, filtered by the level the client set via
        /// <c>logging/setLevel</c>. A no-op when logging was not enabled on the server, or when the message
        /// is less severe than the client's requested minimum.
        /// </summary>
        public Task Log(LoggingLevel level, Json data, string logger = null)
        {
            if (!_loggingEnabled)
                return Task.CompletedTask;

            var min = _minLogLevel;
            if (min.HasValue && (int)level < (int)min.Value)
                return Task.CompletedTask;

            var message = new LogMessage(level, data, logger);
            return _transport.SendNotification(new JsonRpcNotification("notifications/message", message.WriteMembers));
        }

        private void OnPromptsListChanged()
        {
            SendNotification("notifications/prompts/list_changed");
        }

        private void OnResourcesListChanged()
        {
            SendNotification("notifications/resources/list_changed");
        }

        private void OnResourceUpdated(string uri)
        {
            SendNotification("notifications/resources/updated", w => w.Write("uri", uri));
        }

        public async Task Start()
        {
            try
            {
                if (_isRunning)
                    return;
              
                RegisterListeners();
                await _transport.Start();
                _isRunning = true;
                _logger.LogDebug("Mcp Server Started");
            }
            catch (Exception)
            {
                UnregisterListeners();
                throw;
            }
        }

        public async Task Stop()
        {
            if (!_isRunning)
                return;

            // Detach from the transport first: no new requests get dispatched, but the read loop is still
            // alive, so ping/cancel for the requests we're about to drain still flow through.
            UnregisterListeners();
            _logger.LogDebug("Stopping Mcp Server...");
            _isRunning = false;

            // Let handlers already producing a response finish and flush before we close the wire.
            var inflightRequestHandler = new List<Task>(_inFlightRequestHandlers.Keys);
            if (inflightRequestHandler.Count > 0)
            {
                _logger.LogDebug($"Draining {inflightRequestHandler.Count} in-flight request(s)...");
                // Faults are already logged inside ProcessRequest; WhenAll only re-surfaces them, so swallow.
                try { await Task.WhenAll(inflightRequestHandler); }
                catch (Exception ex) { _logger.LogError(ex); }
            }

            await _transport.Stop();
            _logger.LogDebug("Mcp Server Stopped");
        }

        private void RegisterListeners()
        {
            if (_toolsController != null)
                _toolsController.ListChanged += ToolsControllerOnListChanged;

            if (_promptController != null && _promptController.IsListChangedNotificationSupported)
                _promptController.ListChanged += OnPromptsListChanged;

            if (_resourcesController != null)
            {
                if (_resourcesController.IsListChangedNotificationSupported == true)
                    _resourcesController.ListChanged += OnResourcesListChanged;
                if (_resourcesController.IsResourceChangedNotificationSupported == true)
                    _resourcesController.ResourceUpdated += OnResourceUpdated;
            }

            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
        }

        private void UnregisterListeners()
        {
            if (_toolsController != null)
                _toolsController.ListChanged -= ToolsControllerOnListChanged;

            if (_promptController != null && _promptController.IsListChangedNotificationSupported)
                _promptController.ListChanged -= OnPromptsListChanged;

            if (_resourcesController != null)
            {
                if (_resourcesController.IsListChangedNotificationSupported == true)
                    _resourcesController.ListChanged -= OnResourcesListChanged;
                if (_resourcesController.IsResourceChangedNotificationSupported == true)
                    _resourcesController.ResourceUpdated -= OnResourceUpdated;
            }

            _transport.RequestReceived -= OnRequestReceived;
            _transport.NotificationReceived -= OnNotificationReceived;
        }

        private async void ToolsControllerOnListChanged()
        {
            try
            {
                await _transport.SendNotification(new JsonRpcNotification("notifications/tools/list_changed", (Json)null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        // Event subscriber for the transport. Stays void (RequestReceivedCallback is void) and synchronous:
        // it starts the handler and returns immediately, so the transport read loop keeps pumping — this is
        // what lets requests run concurrently, and ping/cancel stay responsive while a slow handler is
        // mid-flight. Unlike async-void, the handler's Task is captured here so Stop() can drain it.
        private void OnRequestReceived(JsonRpcRequest request)
        {
            var task = ProcessRequest(request);

            // Completed synchronously (no real await was hit) — nothing to drain, don't touch the set.
            if (task.IsCompleted)
                return;

            _inFlightRequestHandlers[task] = 0;
            task.ContinueWith(
                OnRequestHandlerCompleted,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        // Retires a finished handler from the drain set. ContinueWith hands us the completed antecedent,
        // which is exactly the key OnRequestReceived stored, so we can remove it directly.
        private void OnRequestHandlerCompleted(Task task)
        {
            _inFlightRequestHandlers.TryRemove(task, out _);
        }

        private async Task ProcessRequest(JsonRpcRequest request)
        {
            var requestId = request.Id;
            var path = request.Method;
            var payload = request.Parameters;

            // Make the request cancellable (via notifications/cancelled) and progress-reportable (via the
            // request's _meta.progressToken) through an McpRequestContext passed explicitly to the handler.
            var cts = new CancellationTokenSource();
            _inFlightRequests[requestId] = cts;
            try
            {
                // Build the context inside the try: reading _meta.progressToken can fail on a malformed
                // token, and that must produce an error response (and run the finally cleanup) rather than
                // escape this handler, which would hang the request and leak the cts.
                var context = new McpRequestContext(
                    cts.Token,
                    new TransportProgressReporter(_transport, ReadProgressToken(payload)));

                _logger.LogDebug($"Received Request: Id: {requestId}, Method: {path}, Payload: {payload}");

                if (!_initialized && path != "initialize" && path != "ping")
                    throw new McpErrorException(ErrorCode.InvalidRequest, "Server not initialized; send 'initialize' first");

                if (_requestHandlersByPathLookup.TryGetValue(path, out var requestHandler))
                    await requestHandler.Invoke(requestId, payload, context);
                else
                    await TrySendError(requestId, ErrorCode.MethodNotFound, $"Method '{path}' is not supported");
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                // Cancelled via notifications/cancelled. The spec permits dropping the response, which is
                // what we do — the canceller is no longer waiting for it.
                _logger.LogDebug($"Request {requestId} ({path}) cancelled; response suppressed");
            }
            catch (McpErrorException ex)
            {
                // A handler asked for a specific JSON-RPC error (e.g. InvalidParams for malformed params),
                // rather than letting it collapse into a generic InternalError below.
                await TrySendError(requestId, ex.Code, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
                await TrySendError(requestId, ErrorCode.InternalError, "Internal server error");
            }
            finally
            {
                _inFlightRequests.TryRemove(requestId, out _);
                cts.Dispose();
            }
        }

        // Sends an error response, swallowing transport failures: during shutdown (or against a vanished
        // peer) the write can throw, and there's nowhere left to report it. Keeping ProcessRequest
        // non-faulting means the drain in Stop() never has to special-case a faulted handler task.
        private async Task TrySendError(RequestId requestId, ErrorCode code, string message)
        {
            try
            {
                await _transport.SendResponse(JsonRpcResponse.Failure(requestId, new Error(code, message)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }

        /// <summary>Reads an optional progress token from a request's <c>_meta.progressToken</c>.</summary>
        private static RequestId? ReadProgressToken(IJsonObject payload)
        {
            try
            {
                var token = payload?["_meta"]?.AsObject()?["progressToken"];
                return token == null ? null : RequestId.FromJson(token);
            }
            catch
            {
                // A malformed optional progressToken must not fail the request; just disable progress for it.
                return null;
            }
        }

        /// <summary>
        /// Returns the named request param, or throws <see cref="McpErrorException"/> with
        /// <see cref="ErrorCode.InvalidParams"/> when the params object or the field is absent — so the
        /// client gets a -32602, not a generic -32603, for a malformed request.
        /// </summary>
        private static IJsonProperty RequireField(IJsonObject payload, string field)
        {
            if (payload == null)
                throw new McpErrorException(ErrorCode.InvalidParams, "Missing required params");
            return payload[field]
                ?? throw new McpErrorException(ErrorCode.InvalidParams, $"Missing required parameter '{field}'");
        }

        private Task HandlePingRequest(RequestId requestId, IJsonObject reqPayload, McpRequestContext context)
            => _transport.SendResponse(JsonRpcResponse.Ok(requestId, _ => { }));

        private Task HandleSetLevelRequest(RequestId requestId, IJsonObject reqPayload, McpRequestContext context)
        {
            var levelText = RequireField(reqPayload, "level").AsString();
            if (!LoggingLevelExtensions.TryParse(levelText, out var level))
                throw new McpErrorException(ErrorCode.InvalidParams, $"Unknown logging level '{levelText}'");

            _minLogLevel = level;
            return _transport.SendResponse(JsonRpcResponse.Ok(requestId, _ => { }));
        }

        private async Task HandleInitializeRequest(RequestId requestId, IJsonObject reqPayload, McpRequestContext context)
        {
            if (_initialized)
                throw new McpErrorException(ErrorCode.InvalidRequest, "Server already initialized");

            var request = new InitializeRequest(reqPayload);

            // Negotiate: honour the client's requested version when we support it,
            // otherwise offer our latest. We never error on a version mismatch.
            var negotiatedVersion = ProtocolVersion.IsSupported(request.ProtocolVersion)
                ? request.ProtocolVersion
                : ProtocolVersion.Latest;

            _initialized = true;

            var result = new InitializeResult(negotiatedVersion, _capabilities, _serverInfo);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleListToolsRequest(RequestId requestId, IJsonObject reqPayload, McpRequestContext context)
        {
            var result = await _toolsController.ListTools(new ListToolsRequest(reqPayload), context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleCallToolRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            var result = await _toolsController.CallTool(new CallToolRequest(arguments), context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleListPromptsRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            var result = await _promptController.ListPrompts(new ListPromptsRequest(arguments), context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleGetPromptRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            RequireField(arguments, "name");
            var result = await _promptController.GetPrompt(new GetPromptRequest(arguments), context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleListResourceTemplatesRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            var result = await _resourcesController.ListTemplates(new ListTemplatesRequest(arguments), context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleReadResourceRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            RequireField(arguments, "uri");
            var result = await _resourcesController.ReadResource(new ReadResourceRequest(arguments), context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleListResourcesRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            var result = await _resourcesController.ListResources(new ListResourcesRequest(arguments), context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, result.WriteMembers));
        }

        private async Task HandleSubscribeRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            var uri = RequireField(arguments, "uri").AsString();
            await _resourcesController.Subscribe(uri, context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, _ => { }));
        }

        private async Task HandleUnsubscribeRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            var uri = RequireField(arguments, "uri").AsString();
            await _resourcesController.Unsubscribe(uri, context);
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, _ => { }));
        }

        private async Task HandleCompleteRequest(RequestId requestId, IJsonObject arguments, McpRequestContext context)
        {
            RequireField(arguments, "ref");
            RequireField(arguments, "argument");
            var result = await _completionController.Complete(new CompletionRequest(arguments), context);
            // The CompleteResult nests the suggestions under a "completion" object, per spec.
            await _transport.SendResponse(JsonRpcResponse.Ok(requestId, w => w.Write("completion", result)));
        }
        
        private void OnNotificationReceived(JsonRpcNotification notification)
        {
            _logger.LogDebug($"Received Notification: {notification.Method}, {notification.Parameters}");

            // Cancel an in-flight request when the client asks us to stop processing it.
            if (notification.Method == "notifications/cancelled" && notification.Parameters != null)
            {
                var idProperty = notification.Parameters["requestId"];
                if (idProperty != null && _inFlightRequests.TryGetValue(RequestId.FromJson(idProperty), out var cts))
                    cts.Cancel();
            }
        }

        private async void SendNotification(string notification, Json arguments = null)
        {
            try
            {
                await _transport.SendNotification(new JsonRpcNotification(notification, arguments));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex);
            }
        }
    }
}