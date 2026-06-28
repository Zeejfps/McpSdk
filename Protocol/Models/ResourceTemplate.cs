namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// A parameterised resource the server can expand (<c>resources/templates/list</c>).
    /// <c>UriTemplate</c> is an RFC 6570 template; carries the 2025-06-18 <c>title</c> and the
    /// 2025-11-25 <c>icons</c> + <c>_meta</c>.
    /// </summary>
    public sealed class ResourceTemplate : IJsonObjectWriter
    {
        public string UriTemplate { get; set; }
        public string Name { get; set; }

        /// <summary>Human-friendly display title (2025-06-18); falls back to Name when absent.</summary>
        public string Title { get; set; }

        public string Description { get; set; }
        public string MimeType { get; set; }

        /// <summary>Optional display icons (2025-11-25).</summary>
        public Icon[] Icons { get; set; }

        /// <summary>Opaque, implementation-defined metadata.</summary>
        public Meta Meta { get; set; }

        public ResourceTemplate() {}

        public ResourceTemplate(string uriTemplate, string name, string description = null, string mimeType = null)
        {
            UriTemplate = uriTemplate;
            Name = name;
            Description = description;
            MimeType = mimeType;
        }

        public ResourceTemplate(IJsonObject jsonObject)
        {
            UriTemplate = jsonObject["uriTemplate"].AsString();
            Name = jsonObject["name"].AsString();
            Title = jsonObject["title"]?.AsString();
            Description = jsonObject["description"]?.AsString();
            MimeType = jsonObject["mimeType"]?.AsString();

            Icons = jsonObject["icons"].AsArray(o => new Icon(o));

            var metaObj = jsonObject["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("uriTemplate", UriTemplate);
            writer.Write("name", Name);
            Title?.WriteTo(writer, "title");
            Description?.WriteTo(writer, "description");
            MimeType?.WriteTo(writer, "mimeType");
            if (Icons is { Length: > 0 })
                Icons.WriteTo(writer, "icons");
            Meta?.WriteTo(writer, "_meta");
        }
    }
}
