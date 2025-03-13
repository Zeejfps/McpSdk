using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Client
{
    internal sealed class McpClient : IClient
    {
        private readonly ITransport _transport;
        private readonly ClientInfo _clientInfo;
        private readonly IRootsController _roots;
        private readonly ISamplingController _sampling;

        public McpClient(ITransport transport, ClientInfo clientInfo, IRootsController roots, ISamplingController sampling)
        {
            _transport = transport;
            _roots = roots;
            _clientInfo = clientInfo;
            _sampling = sampling;
        }

        private void OnRootsListChanged()
        {
            _transport.SendNotification("notifications/roots/list_changed");
        }

        private void OnRequestReceived(int requestId, string method, IJsonObject args)
        {
            if (method == "roots/list")
            {
                OnListRootsRequestReceived(requestId, args);
            }
            else if (method == "sampling/createMessage")
            {
                OnCreateMessageRequestReceived(requestId, args);
            }
        }

        private async void OnCreateMessageRequestReceived(int requestId, IJsonObject methodParams)
        {
            try
            {
                var sampling = _sampling;
                if (sampling == null)
                    return;
                
                var request = new CreateMessageRequest(methodParams);
                var result = await sampling.CreateMessages(request);
                await _transport.SendOkResponse(requestId, result.AsJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async void OnListRootsRequestReceived(int requestId, IJsonObject _)
        {
            try
            {
                if (_roots == null)
                    return;

                var result = await _roots.ListRoots().ConfigureAwait(false);
                await _transport.SendOkResponse(requestId, result.AsJson).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void OnNotificationReceived(string notification)
        {
            Console.WriteLine($"Notification Received: {notification}");
        }

        public bool IsConnected { get; private set; }
        
        public async Task Connect()
        {
            if (IsConnected)
                throw new Exception("Client is already connected");
            
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
            await _transport.Start();
            
            var clientProtocolVersion = "2024-11-05";
            var capabilities = new ClientCapabilities();
            if (_roots != null)
                capabilities.RootsCapability = new RootsCapability(_roots.IsListChangedNotificationSupported);

            if (_sampling != null)
                capabilities.SamplingCapability = new SamplingCapability();
            
            var initializeRequest = new InitializeRequest(clientProtocolVersion, capabilities, _clientInfo);
            var resultJsonObject = await _transport.SendRequest("initialize", initializeRequest.AsJson);
            var initializeResult = new InitializeResult(resultJsonObject);
            
            var serverProtocolVersion = initializeResult.ProtocolVersion;
            if (serverProtocolVersion != clientProtocolVersion)
                throw new ClientException($"Invalid protocol version. Expected {clientProtocolVersion}, got {serverProtocolVersion}");
            
            await _transport.SendNotification("initialized");
            
            if (_roots != null && _roots.IsListChangedNotificationSupported)
                _roots.ListChanged += OnRootsListChanged;
            
            IsConnected = true;
        }

        public async Task<ListToolsResult> ListTools()
        {
            var result = await _transport.SendRequest("tools/list", payload => { });
            return new ListToolsResult(result);
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request)
        {
            var result = await _transport.SendRequest("tools/call", request.AsJson);
            return new CallToolResult(result);
        }
    }
}