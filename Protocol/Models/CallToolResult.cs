using System;
using System.Linq;

namespace McpSdk.Protocol.Models
{
    public sealed class CallToolResult : IJsonObjectWriter
    {
        public static CallToolResult Ok(params Content[] content)
        {
            return new CallToolResult(content);
        }

        public static CallToolResult Ok(TextContent content)
        {
            return new CallToolResult([content]);
        }

        public static CallToolResult Error(params Content[] content)
        {
            return new CallToolResult(content, true);
        }

        public static CallToolResult Error(TextContent content)
        {
            return new CallToolResult([content], true);
        }

        /// <summary>
        /// Builds a result for a tool that declares an <c>outputSchema</c>. The structured object is
        /// emitted as <c>structuredContent</c> and — for back-compat with clients that only read the
        /// unstructured <c>content</c> array — also serialized into a leading text block (SEP). Any
        /// <paramref name="extraContent"/> is appended after that text block.
        /// </summary>
        public static CallToolResult Structured(IJsonObject structuredContent, params Content[] extraContent)
        {
            var backCompat = new TextContent(structuredContent.ToString());
            var content = extraContent is { Length: > 0 }
                ? new Content[] { backCompat }.Concat(extraContent).ToArray()
                : [backCompat];
            return new CallToolResult(content, isError: null, structuredContent: structuredContent);
        }

        public CallToolResult(Content[] content, bool? isError = null, IJsonObject structuredContent = null, Meta meta = null)
        {
            Content = content;
            IsError = isError;
            StructuredContent = structuredContent;
            Meta = meta;
        }

        public CallToolResult(IJsonObject jsonObject)
        {
            Content = jsonObject["content"].AsArray(Models.Content.FromJsonObject) ?? Array.Empty<Content>();
            IsError = jsonObject["isError"]?.AsBool();
            StructuredContent = jsonObject["structuredContent"]?.AsObject();
            var metaObj = jsonObject["_meta"]?.AsObject();
            if (metaObj != null)
                Meta = new Meta(metaObj);
        }

        public void WriteMembers(IJsonWriter writer)
        {
            Content.WriteTo(writer, "content");
            StructuredContent?.WriteTo(writer, "structuredContent");
            IsError?.WriteTo(writer, "isError");
            Meta?.WriteTo(writer, "_meta");
        }

        public Content[] Content { get; }
        public bool? IsError { get; }

        /// <summary>Arbitrary JSON object matching the tool's declared <c>outputSchema</c>, when present.</summary>
        public IJsonObject StructuredContent { get; }

        public Meta Meta { get; }
    }
}
