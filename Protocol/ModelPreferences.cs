namespace McpSharp.Protocol
{
    public sealed class ModelPreferences : JsonObjectWrapper
    {
        public ModelPreferences(IJsonObject jsonObject) : base(jsonObject)
        {
            var hintObjArray = jsonObject["hints"].AsObjectArray();
            IntelligencePriority = jsonObject["intelligencePriority"]?.AsFloat();
            SpeedPriority = jsonObject["speedPriority"]?.AsFloat();
        }

        public float? IntelligencePriority { get; }
        public float? SpeedPriority { get; }
    }
}