using System;

namespace McpSdk.Client
{
    public class ClientException : Exception
    {
        public ClientException(string message) : base(message)
        {
            
        }
    }
}