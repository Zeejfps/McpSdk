namespace McpSharp.Protocol
{
    public sealed class ListToolsResult
    {
        public ListToolsResult(IJsonObject jsonObject)
        {
            JsonObject = jsonObject;
            
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

        public IJsonObject JsonObject { get; }
        public Tool[] Tools { get; }

        public override string ToString()
        {
            return JsonObject.ToString();
        }
    }
}