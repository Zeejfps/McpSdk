using System;
using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class ListRootsResult
    {
        public ListRootsResult(Root[] roots)
        {
            Roots = roots;
        }

        public ListRootsResult(IJsonObject jsonObject)
        {
            Roots = jsonObject["roots"]
                .AsObjectArray()
                .Select(rootObj => new Root(rootObj))
                .ToArray();
        }

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("roots", Roots
                .Select<Root, Action<IJsonWriter>>(root => root.ToJson)
                .ToArray());
        }
        
        public Root[] Roots { get; }
    }
}