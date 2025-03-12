using System;

namespace McpSdk.Protocol.Models
{
    public sealed class CallToolRequest
    {
        private const string ToolNameProp = "name";
        private const string ArgumentsProp = "arguments";
        
        public CallToolRequest(IJsonObject jsonObject)
        {
            ToolName = jsonObject[ToolNameProp]?.AsString();
            ToolArguments = jsonObject[ArgumentsProp]?.AsObject();
        }
        
        public string ToolName { get; }
        public IJsonObject ToolArguments { get; }
        
        public static Writer CreateWriter(IJsonWriter jsonWriter)
        {
            return new Writer(jsonWriter);
        }
        
        public sealed class Writer
        {
            private readonly IJsonWriter _writer;
            
            internal Writer(IJsonWriter writer)
            {
                _writer = writer;
            }

            public Writer WriteToolName(string toolName)
            {
                _writer.Write(ToolNameProp, toolName);
                return this;
            }

            public Writer WriteArguments(Action<IJsonWriter> writeArguments)
            {
                _writer.Write(ArgumentsProp, writeArguments);
                return this;
            }
        }
    }
}