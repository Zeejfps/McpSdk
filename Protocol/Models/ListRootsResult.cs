using System;
using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class ListRootsResult : IJsonObjectWriter
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

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("roots", Roots);
        }
        
        public Root[] Roots { get; }
    }
}