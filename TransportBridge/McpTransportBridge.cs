using System;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.TransportBridge
{
    public sealed class McpTransportBridge
    {
        private readonly ILogger _logger;
        private readonly ITransport _srcTransport;
        private readonly ITransport _dstTransport;
        private bool _isRunning;
        
        public McpTransportBridge(ILoggerFactory loggerFactory, ITransport srcTransport, ITransport dstTransport)
        {
            _logger = loggerFactory.Create<McpTransportBridge>();
            
            _srcTransport = srcTransport;
            _srcTransport.RequestReceived += SrcTransport_OnRequestReceived;
            
            _dstTransport = dstTransport;
            _dstTransport.RequestReceived += DstTransport_OnRequestReceived;
        }

        private async void SrcTransport_OnRequestReceived(int requestId, string method, IJsonObject arguments)
        {
            try
            {
                _logger.LogDebug("SrcTransport_OnRequestReceived");
                var response = await _dstTransport.SendRequest(method, arguments.AsJson);
                if (response.IsOk)
                {
                    var result = response.Result;
                    await _srcTransport.SendOkResponse(requestId, result.AsJson);
                }
                else
                {
                    var error = response.Error;
                    await _srcTransport.SendErrorResponse(requestId, error);
                }
            }
            catch (Exception ex)
            {
                await _srcTransport.SendErrorResponse(requestId, new Error(ErrorCode.InternalError, ex.Message));
            }
        }

        private async void DstTransport_OnRequestReceived(int requestId, string method, IJsonObject arguments)
        {
            try
            {
                _logger.LogDebug("DstTransport_OnRequestReceived");
                var response = await _srcTransport.SendRequest(method, arguments.AsJson);
                if (response.IsOk)
                {
                    var result = response.Result;
                    await _dstTransport.SendOkResponse(requestId, result.AsJson);
                }
                else
                {
                    var error = response.Error;
                    await _dstTransport.SendErrorResponse(requestId, error);
                }
            }
            catch (Exception ex)
            {
                await _dstTransport.SendErrorResponse(requestId, new Error(ErrorCode.InternalError, ex.Message));
            }
        }

        public async Task Run()
        {
            _logger.LogDebug("Starting...");
            var startUnityTransportTask = _dstTransport.Start();
            var startStdioTransportTask = _srcTransport.Start();
            await Task.WhenAll(startUnityTransportTask, startStdioTransportTask);
            _logger.LogDebug("Started");
            while (_isRunning)
            {
                await Task.Delay(2000);
            }
            _logger.LogDebug("Stopped");
        }

        public async Task Interrupt()
        {
            _isRunning = false;
            _logger.LogDebug("Stopping...");
            await Task.WhenAll(_srcTransport.Stop(), _dstTransport.Stop());
        }
    }
}