using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class ModelPreferences : IJsonObjectWriter
    {
        public ModelHint[] Hints { get; }
        public float? IntelligencePriority { get; }
        public float? SpeedPriority { get; }
        
        public ModelPreferences(IJsonObject jsonObject)
        {
            Hints = jsonObject["hints"]?
                .AsObjectArray()
                .Select(hint => new ModelHint(hint))
                .ToArray();
            IntelligencePriority = jsonObject["intelligencePriority"]?.AsFloat();
            SpeedPriority = jsonObject["speedPriority"]?.AsFloat();
        }

        public void WriteMembers(IJsonWriter writer)
        {
            Hints.WriteTo(writer, "hints");
            
            IntelligencePriority?.WriteTo(writer, "intelligencePriority");
            SpeedPriority?.WriteTo(writer, "speedPriority");
        }
    }
}