using System;
using System.Linq;
using System.Threading.Tasks;
using McpSharp.Protocol;

namespace McpSharp.Client
{
    internal sealed class McpClient : IClient
    {
        private readonly ITransport _transport;
        private readonly ClientInfo _clientInfo;
        private readonly IRootsCapability _roots;
        private readonly ISamplingCapability _sampling;

        public McpClient(ITransport transport, ClientInfo clientInfo, IRootsCapability roots, ISamplingCapability sampling)
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
            Console.WriteLine($"Request Received: {method}");
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
                
                var request = new CreateMessageParams(methodParams);
                var result = await sampling.CreateMessages(request);
                await _transport.SendResponse(requestId, payload =>
                {
                    payload.Write("role", result.Role);
                    payload.Write("model", result.Model);
                    payload.Write("stopReason", result.StopReason);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private async void OnListRootsRequestReceived(int requestId, IJsonObject args)
        {
            try
            {
                if (_roots == null)
                    return;

                var listRootsResult = await _roots.ListRoots().ConfigureAwait(false);
                await _transport.SendResponse(requestId, payload =>
                {
                    var roots = listRootsResult.Roots.Select<Root, Action<IJsonWriter>>(root =>
                    {
                        return element =>
                        {
                            element.Write("uri", root.Uri);
                            element.Write("name", root.Name);
                        };
                    }).ToArray();

                    payload.Write("roots", roots);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void OnNotificationReceived(string notification)
        {
            
        }

        public bool IsConnected { get; private set; }
        
        public async Task Connect()
        {
            if (IsConnected)
                throw new Exception("Client is already connected");
            
            await _transport.Connect();
            _transport.RequestReceived += OnRequestReceived;
            _transport.NotificationReceived += OnNotificationReceived;
            
            var clientProtocolVersion = "2024-11-05";
            var response = await _transport.SendRequest("initialize", payload =>
            {
                payload.Write("protocolVersion", "2024-11-05");
                payload.Write("capabilities", capabilities =>
                {
                    if (_roots != null)
                    {
                        payload.Write("roots", roots =>
                        {
                            roots.Write("listChanged", _roots.IsListChangedNotificationSupported);
                        });
                    }

                    if (_sampling != null)
                    {
                        payload.Write("sampling", sampling => { });
                    }
                });
                payload.Write("clientInfo", clientInfo =>
                {
                    payload.Write("name", _clientInfo.Name);
                    payload.Write("version", _clientInfo.Version);
                });
            });
            
            var serverProtocolVersion = response["protocolVersion"].AsString();
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

        public async Task<CallToolResult> CallTool(string toolName, Action<IJsonWriter> args)
        {
            var result = await _transport.SendRequest("tools/call", payload =>
            {
                payload.Write("name", toolName);
                payload.Write("arguments", bodyWriter =>
                {
                    args?.Invoke(bodyWriter);
                });
            });
            return new CallToolResult(result);
        }
    }
}