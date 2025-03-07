using System;

namespace McpSharp.Client
{
    public class ClientException : Exception
    {
        public ClientException(string message) : base(message)
        {
            
        }
    }
}