namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// The reserved <c>_meta</c> field that may appear on any request's params, any result, or any
    /// notification's params. Its contents are implementation-defined, so it is carried as an opaque
    /// JSON object so peers can pass metadata through without this SDK needing to model every key.
    /// </summary>
    public sealed class Meta
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

        /// <summary>
        /// Reads the <c>_meta</c> object out of the given parent (a params or result object),
        /// returning null when it is absent.
        /// </summary>
        public static Meta From(IJsonObject parent)
        {
            var meta = parent?["_meta"]?.AsObject();
            return meta == null ? null : new Meta(meta);
        }

        public void AsJson(IJsonWriter writer)
        {
            _data.AsJson(writer);
        }
    }
}
