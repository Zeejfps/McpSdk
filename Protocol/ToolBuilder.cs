using System;
using System.Collections.Generic;
using McpSdk.Protocol.Models;

namespace McpSdk.Protocol
{
    public sealed class ToolBuilder
    {
        private readonly IJson _json;
        private readonly List<Action<IJsonWriter>> _inputs = new List<Action<IJsonWriter>>();

        private string _name;
        private string _description;
        private IJsonObject _inputSchema;

        public ToolBuilder(IJson json)
        {
            _json = json;
        }

        public ToolBuilder Name(string name)
        {
            _name = name;
            return this;
        }

        public ToolBuilder Description(string description)
        {
            _description = description;
            return this;
        }

        public ToolBuilder Input(string name, Action<InputSchemaWriter> input)
        {
            var writeInputSchemaAction = new Action<IJsonWriter>(writer =>
            {
                writer.Write(name, props =>
                {
                    input.Invoke(new InputSchemaWriter(props));
                });
            });
            _inputs.Add(writeInputSchemaAction);
            return this;
        }

        public Tool Build()
        {
            _inputSchema = _json.Build(props =>
            {
                foreach (var writeInputs in _inputs)
                    writeInputs(props);
            });
            return new Tool(_json, _name, _description, _inputSchema);
        }
    }
    
    public sealed class InputSchemaWriter
    {
        private readonly IJsonWriter _writer;

        public InputSchemaWriter(IJsonWriter writer)
        {
            _writer = writer;
        }

        public NumberInputSchemaWriter Number()
        {
            return new NumberInputSchemaWriter(_writer);
        }
    }

    public sealed class NumberInputSchemaWriter
    {
        private readonly IJsonWriter _jsonObject;

        public NumberInputSchemaWriter(IJsonWriter jsonObject)
        {
            _jsonObject = jsonObject;
        }

        public NumberInputSchemaWriter Min(int minValue)
        {
            _jsonObject.Write("min", minValue);
            return this;
        }

        public NumberInputSchemaWriter Max(int maxValue)
        {
            _jsonObject.Write("max", maxValue);
            return this;
        }

        public NumberInputSchemaWriter Describe(string description)
        {
            _jsonObject.Write("description", description);
            return this;
        }
    }
    
}