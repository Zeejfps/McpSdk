#nullable disable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Cursor-based pagination: the opaque-cursor helper round-trips offsets (and rejects junk), a
    /// paginating <c>tools/list</c> walks every page via <c>nextCursor</c> returning each tool exactly
    /// once and a null cursor on the final page, and a non-paginating controller returns everything in one
    /// page with no cursor.
    /// </summary>
    public sealed class PaginationTests : ConformanceSuite
    {
        public PaginationTests(TestReport report) : base(report) { }

        public override string Title => "Pagination (opaque cursors)";

        public override async Task Run()
        {
            await Test("opaque cursor round-trips an offset and rejects junk", CursorRoundTrips);
            await Test("tools/list walks every page via nextCursor (each tool once)", ToolsListPaginates);
            await Test("non-paginating tools/list returns one page with no cursor", ToolsListSinglePageHasNoCursor);
        }

        private Task CursorRoundTrips()
        {
            foreach (var offset in new[] { 0, 1, 7, 42, 100000 })
            {
                var encoded = PaginationCursor.EncodeOffset(offset);
                var ok = PaginationCursor.TryDecodeOffset(encoded, out var decoded);
                Assert(ok, $"cursor for offset {offset} decodes");
                Assert(decoded == offset, $"cursor for offset {offset} round-trips (got {decoded})");
            }

            Assert(!PaginationCursor.TryDecodeOffset(null, out _), "null cursor is rejected");
            Assert(!PaginationCursor.TryDecodeOffset("", out _), "empty cursor is rejected");
            Assert(!PaginationCursor.TryDecodeOffset("not-base64!!", out _), "malformed cursor is rejected");

            return Task.CompletedTask;
        }

        private async Task ToolsListPaginates()
        {
            const int toolCount = 5;
            const int pageSize = 2;

            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            var server = BuildPaginatingServer(serverEnd, pageSize, toolCount);
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            var names = new List<string>();
            var pageLengths = new List<int>();
            string cursor = null;
            var pages = 0;

            do
            {
                var page = await client.ListTools(new ListToolsRequest(cursor));
                pages++;
                pageLengths.Add(page.Tools.Length);
                names.AddRange(page.Tools.Select(t => t.Name));
                cursor = page.NextCursor;
            }
            while (cursor != null && pages < 100);

            Assert(cursor == null, "pagination terminates with a null nextCursor on the final page");
            Assert(pages == 3, $"5 tools at page size 2 spans 3 pages (got {pages})");
            Assert(names.Count == toolCount, $"every tool is returned across the pages (got {names.Count})");
            Assert(names.Distinct().Count() == toolCount, "no tool is returned twice across pages");
            Assert(pageLengths.Take(pageLengths.Count - 1).All(n => n == pageSize),
                "every non-final page is full (== page size)");
            Assert(pageLengths.Last() <= pageSize, "the final page is no larger than the page size");
        }

        private async Task ToolsListSinglePageHasNoCursor()
        {
            var (clientEnd, serverEnd) = InMemoryTransport.CreatePair(Json, Loggers);
            // BuildServer leaves PageSize null, so all tools come back in one page.
            var server = BuildServer(serverEnd);
            await server.Start();

            var client = ConnectClient(clientEnd);
            await client.Connect();

            var page = await client.ListTools();

            Assert(page.NextCursor == null, "a non-paginating list has no nextCursor");
            Assert(page.Tools.Length >= 2, "the single page carries all registered tools");
        }

        private IServer BuildPaginatingServer(InMemoryTransport serverEnd, int pageSize, int toolCount)
        {
            return new ServerBuilder()
                .WithName("Page Server")
                .WithVersion("1.0.0")
                .WithTransport(new FixedTransportFactory(serverEnd))
                .WithDefaultToolsCapability(Json, tools =>
                {
                    tools.PageSize = pageSize;
                    for (var i = 0; i < toolCount; i++)
                        tools.AddTool(new NoOpToolHandler($"tool-{i:D2}"));
                })
                .Build();
        }

        /// <summary>A minimal tool with a unique name, used only to populate a list for paging.</summary>
        private sealed class NoOpToolHandler : IToolHandler
        {
            public Tool Tool { get; }

            public NoOpToolHandler(string name)
            {
                Tool = new Tool(name, "no-op", new ObjectSchema());
            }

            public Task<CallToolResult> Call(IJsonObject arguments, McpRequestContext context)
            {
                return Task.FromResult(new CallToolResult(new Content[] { new TextContent("ok") }, false));
            }
        }
    }
}
