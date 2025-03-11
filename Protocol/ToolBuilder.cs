using System;

namespace McpSdk.Protocol
{
    public sealed class ToolBuilder
    {
        private readonly IJson _json;
        
        public ToolBuilder(IJson json)
        {
            _json = json;
        }

        public ToolBuilder Name(string name)
        {
            return this;
        }

        public ToolBuilder Description(string description)
        {
            return this;
        }

        public ToolBuilder Input(string name, Action<InputBuilder> input)
        {
            return this;
        }

        public Tool Build()
        {
            return null;
        }
    }
    
    public sealed class InputBuilder
    {
        public NumberInputBuilder Number()
        {
            return new NumberInputBuilder();
        }
    }

    public sealed class NumberInputBuilder
    {
        public NumberInputBuilder Min(int minValue)
        {
            return this;
        }

        public NumberInputBuilder Max(int maxValue)
        {
            return this;
        }

        public NumberInputBuilder Describe(string description)
        {
            return this;
        }
    }
    
}