using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class ListRootsResult : JsonObjectWrapper
    {
        public ListRootsResult(IJson json, Root[] roots)
        {
            Roots = roots;
            JsonObject = json.Object(prop =>
            {
                prop.Write("roots", Roots.Select(root => root.JsonObject).ToArray());
            });
        }
        
        public Root[] Roots { get; }
        public override IJsonObject JsonObject { get; }
    }
}