namespace McpSdk.Protocol.Models
{
    public sealed class ListToolsResult : IJsonSerializable
    {
        public Tool[] Tools { get; }
        
        public ListToolsResult(Tool[] tools)
        {
            Tools = tools;
        }

        public ListToolsResult(IJsonObject jsonObject)
        {
            var toolsArray = jsonObject["tools"].AsObjectArray();
            var toolsCount = toolsArray.Length;
            var tools = new Tool[toolsCount];
            for (var i = 0; i < toolsCount; i++)
            {
                var toolObj = toolsArray[i];
                tools[i] = new Tool(toolObj);
            }
            Tools = tools;
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("tools", Tools);
        }
    }
}