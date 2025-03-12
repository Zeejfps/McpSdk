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

        public ToolBuilder Input(string name, Action<SchemaWriter> input)
        {
            var writeInputSchemaAction = new Action<IJsonWriter>(writer =>
            {
                writer.Write(name, props =>
                {
                    input.Invoke(new SchemaWriter(props));
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
            Console.WriteLine(_inputSchema);
            return new Tool(_json, _name, _description, _inputSchema);
        }
    }
}