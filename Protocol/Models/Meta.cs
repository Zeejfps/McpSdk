namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// The reserved <c>_meta</c> field that may appear on any request's params, any result, or any
    /// notification's params. Its contents are implementation-defined, so it is carried as an opaque
    /// JSON object so peers can pass metadata through without this SDK needing to model every key.
    /// </summary>
    public sealed class Meta : IJsonSerializable
    {
        private readonly IJsonObject _data;

        public Meta(IJsonObject data)
        {
            _data = data;
        }

        /// <summary>The raw underlying JSON object.</summary>
        public IJsonObject Raw => _data;

        /// <summary>Reads a value out of the metadata, or null if absent.</summary>
        public IJsonProperty this[string key] => _data?[key];

        public void AsJson(IJsonWriter writer)
        {
            _data.AsJson(writer);
        }
    }
}
