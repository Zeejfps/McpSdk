using System;
using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class ModelPreferences
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

        public void ToJson(IJsonWriter writer)
        {
            writer.Write("hints", Hints
                .Select<ModelHint, Action<IJsonWriter>>(hint => hint.ToJson)
                .ToArray());
            
            if (IntelligencePriority.HasValue)
                writer.Write("intelligencePriority", IntelligencePriority.Value);
            
            if (SpeedPriority.HasValue)
                writer.Write("speedPriority", SpeedPriority.Value);
        }
    }
}