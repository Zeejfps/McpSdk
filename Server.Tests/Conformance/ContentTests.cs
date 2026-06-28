#nullable disable
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// The shared <c>Content</c> model used by tool results, sampling, and prompts: every content type
    /// (text, image, audio, resource_link, embedded resource) is recognized by
    /// <see cref="Content.FromJsonObject"/> and round-trips its fields, and a <c>content</c> property
    /// parses whether it is a single object or an array.
    /// </summary>
    public sealed class ContentTests : ConformanceSuite
    {
        public ContentTests(TestReport report) : base(report) { }

        public override string Title => "Content types";

        public override async Task Run()
        {
            await Test("audio + resource_link content types round-trip", AudioAndResourceLinkRoundTrip);
            await Test("image + embedded resource content types round-trip", ImageAndEmbeddedResourceRoundTrip);
            await Test("content parses as a single object or an array", ContentSingleOrArrayParsing);
        }

        private Task AudioAndResourceLinkRoundTrip()
        {
            var audioJson = Json.Object(new AudioContent("audio/wav", "QUJD").WriteMembers);
            var audio = Content.FromJsonObject(audioJson) as AudioContent;
            Assert(audio != null, "audio content type is recognized by Content.FromJsonObject");
            AssertEqual("audio/wav", audio?.MimeType, "audio mimeType round-trips");
            AssertEqual("QUJD", audio?.Base64EncodedData, "audio data round-trips");

            var linkJson = Json.Object(
                new ResourceLinkContent("file:///x.txt", "x.txt", "X File", "a file", "text/plain").WriteMembers);
            var link = Content.FromJsonObject(linkJson) as ResourceLinkContent;
            Assert(link != null, "resource_link content type is recognized by Content.FromJsonObject");
            AssertEqual("file:///x.txt", link?.Uri, "resource_link uri round-trips");
            AssertEqual("x.txt", link?.Name, "resource_link name round-trips");
            AssertEqual("X File", link?.Title, "resource_link title round-trips");
            AssertEqual("text/plain", link?.MimeType, "resource_link mimeType round-trips");

            return Task.CompletedTask;
        }

        private Task ImageAndEmbeddedResourceRoundTrip()
        {
            var imageJson = Json.Object(new ImageContent("image/png", new byte[] { 1, 2, 3 }).WriteMembers);
            var image = Content.FromJsonObject(imageJson) as ImageContent;
            Assert(image != null, "image content type is recognized by Content.FromJsonObject");
            AssertEqual("image/png", image?.MimeType, "image mimeType round-trips");
            Assert(!string.IsNullOrEmpty(image?.Base64EncodedData), "image data round-trips as base64");

            // An embedded resource (type: resource) wraps ResourceContents; build the wire shape directly
            // since EmbeddedResourceContent only exposes a parse constructor.
            var embeddedJson = Json.Object(w =>
            {
                w.Write("type", "resource");
                w.Write("resource", new TextResourceContents("file:///note.txt", "text/plain", "hello"));
            });
            var embedded = Content.FromJsonObject(embeddedJson) as EmbeddedResourceContent;
            Assert(embedded != null, "embedded resource content type is recognized by Content.FromJsonObject");

            var contents = embedded?.Resource as TextResourceContents;
            Assert(contents != null, "embedded resource carries text resource contents");
            AssertEqual("file:///note.txt", contents?.Uri, "embedded resource uri round-trips");
            AssertEqual("text/plain", contents?.MimeType, "embedded resource mimeType round-trips");
            AssertEqual("hello", contents?.Text, "embedded resource text round-trips");

            return Task.CompletedTask;
        }

        private Task ContentSingleOrArrayParsing()
        {
            var single = Json.Object(w => w.Write("content", new TextContent("hi")));
            var one = single["content"].AsArrayOrSingle(Content.FromJsonObject);
            Assert(one.Length == 1 && one[0] is TextContent, "a single content object parses to one block");

            var array = Json.Object(w => w.Write("content", new Content[] { new TextContent("a"), new TextContent("b") }));
            var many = array["content"].AsArrayOrSingle(Content.FromJsonObject);
            Assert(many.Length == 2, "a content array parses to many blocks");

            var none = Json.Object(w => w.Write("role", "user"));
            var empty = none["content"].AsArrayOrSingle(Content.FromJsonObject);
            Assert(empty.Length == 0, "an absent content property parses to an empty array");

            return Task.CompletedTask;
        }
    }
}
