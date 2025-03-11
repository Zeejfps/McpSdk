namespace McpSharp.Protocol
{
    public sealed class ListToolsResult : JsonObjectWrapper
    {
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
            JsonObject = jsonObject;
        }
        
        public Tool[] Tools { get; }
        public override IJsonObject JsonObject { get; }
    }
}