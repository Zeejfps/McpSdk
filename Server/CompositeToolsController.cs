using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using McpSdk.Protocol;
using McpSdk.Protocol.Models;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// Internal marker under which each <c>AddToolsCapability(...)</c> call registers exactly one <i>leaf</i>
    /// tools controller. The public <see cref="IToolsController"/> is never registered as a leaf — it is the
    /// <see cref="CompositeToolsController"/>, which resolves every leaf via <c>GetServices&lt;IToolsControllerSource&gt;()</c>.
    /// Keeping leaves under a distinct marker type avoids the last-wins collision and self-recursion that
    /// would arise from registering both the leaves and the composite under <see cref="IToolsController"/>
    /// (implementation-plan decision #2).
    /// </summary>
    internal interface IToolsControllerSource
    {
        /// <summary>The leaf controller this source contributes to the merged set.</summary>
        IToolsController Controller { get; }

        /// <summary>The leaf's configured page size, if any, used to derive the composite's effective page size.</summary>
        int? PageSize { get; }
    }

    /// <summary>The trivial <see cref="IToolsControllerSource"/> implementation produced by each capability registration.</summary>
    internal sealed class ToolsControllerSource : IToolsControllerSource
    {
        public ToolsControllerSource(IToolsController controller, int? pageSize)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            PageSize = pageSize;
        }

        public IToolsController Controller { get; }
        public int? PageSize { get; }
    }

    /// <summary>
    /// The public <see cref="IToolsController"/> that merges every leaf controller registered under
    /// <see cref="IToolsControllerSource"/> (implementation-plan decision #2). Leaves are read once, in
    /// registration order, from the provider it is resolved in — on a per-session child provider that yields
    /// the root leaves first, then the session's leaves, so session tools are <i>overlaid</i> on the shared
    /// root set and win on name conflicts. <c>tools/list</c> pages over the merged set (reusing the
    /// <see cref="DefaultToolsController"/> offset-cursor semantics); <c>tools/call</c> routes to the leaf
    /// that owns the named tool. When no leaf exists the composite is never registered, so the server's
    /// null-tolerant controller probe returns <c>null</c> and the tools capability is not advertised.
    /// </summary>
    internal sealed class CompositeToolsController : IToolsController, IDisposable
    {
        private readonly List<IToolsControllerSource> _sources;
        private readonly int? _pageSize;

        private MergedSet _merged;
        private bool _disposed;

        public event Action ListChanged;
        public bool IsListChangedNotificationSupported { get; }

        public CompositeToolsController(IServiceProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            // Registrations are frozen once the provider is built, so snapshot the leaves once. On a child
            // provider GetServices returns the parent's leaves first, then this child's (root ∪ session).
            _sources = new List<IToolsControllerSource>(provider.GetServices<IToolsControllerSource>());

            // Effective page size: the first leaf that declares a positive one (root before session). A single
            // leaf — the common case — therefore reproduces that leaf's paging exactly.
            int? pageSize = null;
            var supportsListChanged = false;
            foreach (var source in _sources)
            {
                if (pageSize == null && source.PageSize is > 0)
                    pageSize = source.PageSize;

                if (source.Controller.IsListChangedNotificationSupported)
                    supportsListChanged = true;

                source.Controller.ListChanged += OnLeafListChanged;
            }

            _pageSize = pageSize;
            IsListChangedNotificationSupported = supportsListChanged;
        }

        private void OnLeafListChanged()
        {
            Volatile.Write(ref _merged, null);
            if (IsListChangedNotificationSupported)
                ListChanged?.Invoke();
        }

        public async Task<ListToolsResult> ListTools(ListToolsRequest request, McpRequestContext context)
        {
            var merged = await GetMerged(context);
            return PaginationCursor.GetPage(merged.Ordered, request?.Cursor, _pageSize, m => m.Tool);
        }

        public async Task<CallToolResult> CallTool(CallToolRequest request, McpRequestContext context)
        {
            var merged = await GetMerged(context);
            if (merged.ByName.TryGetValue(request.ToolName, out var entry))
                return await entry.Owner.CallTool(request, context);

            return CallToolResult.Error($"No tool found with name: {request.ToolName}");
        }

        private async Task<MergedSet> GetMerged(McpRequestContext context)
        {
            var cached = Volatile.Read(ref _merged);
            if (cached != null)
                return cached;

            var built = await GatherTools(context);
            Volatile.Write(ref _merged, built);
            return built;
        }

        private async Task<MergedSet> GatherTools(McpRequestContext context)
        {
            var order = new List<string>();
            var byName = new Dictionary<string, MergedTool>();

            foreach (var source in _sources)
            {
                var controller = source.Controller;
                string cursor = null;
                do
                {
                    var pageResult = await controller.ListTools(new ListToolsRequest(cursor), context);
                    if (pageResult?.Tools != null)
                    {
                        foreach (var tool in pageResult.Tools)
                        {
                            if (!byName.ContainsKey(tool.Name))
                                order.Add(tool.Name);
                            byName[tool.Name] = new MergedTool(tool, controller);
                        }
                    }
                    cursor = pageResult?.NextCursor;
                }
                while (cursor != null);
            }

            var ordered = new List<MergedTool>(order.Count);
            foreach (var name in order)
                ordered.Add(byName[name]);
            return new MergedSet(ordered, byName);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            foreach (var source in _sources)
                source.Controller.ListChanged -= OnLeafListChanged;
        }

        private sealed class MergedSet
        {
            public MergedSet(List<MergedTool> ordered, Dictionary<string, MergedTool> byName)
            {
                Ordered = ordered;
                ByName = byName;
            }

            public List<MergedTool> Ordered { get; }
            public Dictionary<string, MergedTool> ByName { get; }
        }

        private readonly struct MergedTool
        {
            public MergedTool(Tool tool, IToolsController owner)
            {
                Tool = tool;
                Owner = owner;
            }

            public Tool Tool { get; }
            public IToolsController Owner { get; }
        }
    }
}
