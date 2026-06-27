using System.Linq;

namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// A displayable icon (2025-11-25), reused across tools, resources and prompts. <c>Src</c> is a
    /// URI — an <c>https://</c> URL or an inline <c>data:</c> URI; <c>MimeType</c> and <c>Sizes</c>
    /// (e.g. <c>"48x48"</c>) are optional hints.
    /// </summary>
    public sealed class Icon : IJsonSerializable
    {
        public string Src { get; set; }
        public string MimeType { get; set; }
        public string Sizes { get; set; }

        public Icon() {}

        public Icon(string src, string mimeType = null, string sizes = null)
        {
            Src = src;
            MimeType = mimeType;
            Sizes = sizes;
        }

        public Icon(IJsonObject jsonObject)
        {
            Src = jsonObject["src"]?.AsString();
            MimeType = jsonObject["mimeType"]?.AsString();
            Sizes = jsonObject["sizes"]?.AsString();
        }

        public void AsJson(IJsonWriter writer)
        {
            writer.Write("src", Src);
            if (MimeType != null)
                writer.Write("mimeType", MimeType);
            if (Sizes != null)
                writer.Write("sizes", Sizes);
        }

        /// <summary>
        /// Maps a raw array of icon objects (e.g. <c>obj["icons"]?.AsObjectArray()</c>) to icons, or
        /// null when the input is null. The caller owns the property key, so the same mapper serves any
        /// parent (tool, resource, prompt).
        /// </summary>
        public static Icon[] ArrayFrom(IJsonObject[] array)
        {
            return array?.Select(icon => new Icon(icon)).ToArray();
        }
    }
}
