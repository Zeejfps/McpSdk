using System.Linq;

namespace McpSdk.Protocol.Models
{
    /// <summary>
    /// A prompt or prompt template the server offers (<c>prompts/list</c>). Carries the 2025-06-18
    /// <c>title</c>, optional <c>arguments</c>, and the 2025-11-25 <c>icons</c> + <c>_meta</c>.
    /// </summary>
    public sealed class Prompt : IJsonObjectWriter
    {
        public string Name { get; set; }

        /// <summary>Human-friendly display title (2025-06-18); falls back to Name when absent.</summary>
        public string Title { get; set; }

        public string Description { get; set; }

        /// <summary>The arguments this prompt accepts, or null when it takes none.</summary>
        public PromptArgument[] Arguments { get; set; }

        /// <summary>Optional display icons (2025-11-25).</summary>
        public Icon[] Icons { get; set; }

        /// <summary>Opaque, implementation-defined metadata.</summary>
        public Meta Meta { get; set; }

        public Prompt() {}

        public Prompt(string name, string description = null, PromptArgument[] arguments = null)
        {
            Name = name;
            Description = description;
            Arguments = arguments;
        }

        public Prompt(IJsonObject jsonObject)
        {
            Name = jsonObject["name"].AsString();
            Title = jsonObject["title"]?.AsString();
            Description = jsonObject["description"]?.AsString();

            Arguments = jsonObject["arguments"]?.AsObjectArray()
                ?.Select(arg => new PromptArgument(arg)).ToArray();

            Icons = Icon.ArrayFrom(jsonObject["icons"]?.AsObjectArray());

            var metaObj = jsonObject["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            writer.Write("name", Name);
            Title?.WriteTo(writer, "title");
            Description?.WriteTo(writer, "description");
            if (Arguments is { Length: > 0 })
                Arguments.WriteTo(writer, "arguments");
            if (Icons is { Length: > 0 })
                Icons.WriteTo(writer, "icons");
            Meta?.WriteTo(writer, "_meta");
        }
    }
}
