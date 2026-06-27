using System.Linq;

namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// A displayable icon (2025-11-25), reused across tools, resources and prompts. <c>Src</c> is a
    /// URI — an <c>https://</c> URL or an inline <c>data:</c> URI; <c>MimeType</c> and <c>Sizes</c>
    /// (e.g. <c>"48x48"</c>) are optional hints.
    /// </summary>
    public sealed class Icon
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

        /// <summary>Reads an <c>icons</c> array off a parent object, or null when absent.</summary>
        public static Icon[] ArrayFrom(IJsonObject parent)
        {
            var icons = parent?["icons"]?.AsObjectArray();
            if (icons == null)
                return null;
            return icons.Select(icon => new Icon(icon)).ToArray();
        }

        /// <summary>Writes an <c>icons</c> array onto the given writer when non-empty.</summary>
        public static void WriteArray(IJsonWriter writer, Icon[] icons)
        {
            if (icons == null || icons.Length == 0)
                return;
            writer.Write("icons", icons.Select<Icon, Json>(icon => icon.AsJson).ToArray());
        }
    }
}
