using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Server;

namespace McpSdk.Adapter.SseServer
{
    internal sealed class SseChannel : ISseChannel
    {
        public event Action ClientConnected;
        public event Action<string> MessageReceived;

        private Task _listenForDisconnect;
        private StreamWriter _textWriter;
        private CancellationTokenSource _cts;
        private HttpListenerResponse _response;

        public void Establish(HttpListenerResponse response)
        {
            _response = response;
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");
            _textWriter = new StreamWriter(response.OutputStream);
            _cts = new CancellationTokenSource();
            _listenForDisconnect = ListenForDisconnect();
            ClientConnected?.Invoke();
        }

        public void Terminate()
        {
            _cts.Cancel();
            _cts.Dispose();
            
            try
            {
                _textWriter.Close();
                _textWriter.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            try
            {
                _response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public async Task Send(SseEvent sseEvent)
        {
            await _textWriter.WriteLineAsync("event: " + sseEvent.Kind);
            
            if (sseEvent.Id != null)
                await _textWriter.WriteLineAsync("id: " + sseEvent.Id);
            
            if (sseEvent.Data != null)
                await _textWriter.WriteLineAsync("data: " + sseEvent.Data);
        }

        private async Task ListenForDisconnect()
        {
            try
            {
                while (_textWriter.BaseStream.CanWrite && !_cts.IsCancellationRequested)
                {
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                Terminate();
            }
        }

        public async void HandlePostMessage(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string jsonMessage;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                    jsonMessage = await reader.ReadToEndAsync();

                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                
                MessageReceived?.Invoke(jsonMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}