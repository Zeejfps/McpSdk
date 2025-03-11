using System.Linq;

namespace McpSharp.Protocol
{
    public sealed class ListRootsResult : JsonObjectWrapper
    {
        public ListRootsResult(IJson json, Root[] roots)
        {
            Roots = roots;
            JsonObject = json.Build(prop =>
            {
                prop.Write("roots", Roots.Select(root => root.JsonObject).ToArray());
            });
        }
        
        public Root[] Roots { get; }
        public override IJsonObject JsonObject { get; }
    }
}