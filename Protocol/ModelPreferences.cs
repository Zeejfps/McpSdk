using System.Linq;

namespace McpSharp.Protocol
{
    public sealed class ModelPreferences : JsonObjectWrapper
    {
        public ModelPreferences(IJsonObject jsonObject) : base(jsonObject)
        {
            Hints = jsonObject["hints"]?
                .AsObjectArray()
                .Select(hint => new ModelHint(hint))
                .ToArray();
            IntelligencePriority = jsonObject["intelligencePriority"]?.AsFloat();
            SpeedPriority = jsonObject["speedPriority"]?.AsFloat();
        }

        public ModelHint[] Hints { get; }
        public float? IntelligencePriority { get; }
        public float? SpeedPriority { get; }
    }
}