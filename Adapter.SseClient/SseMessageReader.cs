using System;
using McpSdk.Client;

namespace McpSdk.Adapter.SseClient
{
    internal sealed class SseMessageReader
    {
        private SseEvent? _currentMessage;

        public event Action<ISseEvent>? EventReceived;
    
        public void ProcessLine(string? line)
        {
            //Console.WriteLine($"Processing {line}");
            if (string.IsNullOrEmpty(line))
            {
                if (_currentMessage != null)
                {
                    EventReceived?.Invoke(_currentMessage);
                    _currentMessage = null;
                }
            
                return;
            }
     
            if (line.StartsWith("event: "))
            {
                var kind = line.Substring(7);
                _currentMessage = new SseEvent(kind);
            }
            else if (line.StartsWith("data: "))
            {
                if (_currentMessage == null)
                {
                    throw new Exception("Expected event, got data");
                }
            
                _currentMessage.Data = line.Substring(6);
            }
            else if (line.StartsWith("id:"))
            {
                if (_currentMessage == null)
                {
                    throw new Exception("Expected event, got id");
                }
            
                _currentMessage.Id = line.Substring(2);
            }
        }
    }
}