using System;
using System.Collections.Generic;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    /// <summary>
    /// The concrete <see cref="IToolsBuilder"/> accumulated by an <c>AddToolsCapability(Action&lt;IToolsBuilder&gt;)</c>
    /// call. It records handler instances, handler <i>types</i>, and an optional page size, then materializes
    /// them into one leaf <see cref="DefaultToolsController"/> via <see cref="BuildLeaf"/> at resolve time.
    /// </summary>
    /// <remarks>
    /// <see cref="BuildLeaf"/> runs inside the leaf's singleton factory, against the container the leaf was
    /// registered in. The serializer is pulled from that scope, and every <c>AddTool&lt;THandler&gt;()</c> type
    /// is activated from it via <see cref="ActivatorUtilities"/>, so a handler's lifetime follows the
    /// registration site: registered on the global builder <c>Context</c> it is one singleton shared across
    /// sessions; registered on a per-session <c>session.Context</c> it is built once per session
    /// (implementation-plan decision #7).
    /// </remarks>
    internal sealed class ToolsBuilder : IToolsBuilder
    {
        private readonly List<IToolHandler> _handlers = new List<IToolHandler>();
        private readonly List<Type> _handlerTypes = new List<Type>();
        private int? _pageSize;

        /// <summary>The configured page size, surfaced to the composite so it pages over the merged set.</summary>
        public int? PageSize => _pageSize;

        public IToolsBuilder AddTool(IToolHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _handlers.Add(handler);
            return this;
        }

        public IToolsBuilder AddTool<THandler>() where THandler : class, IToolHandler
        {
            _handlerTypes.Add(typeof(THandler));
            return this;
        }

        public IToolsBuilder WithPageSize(int pageSize)
        {
            _pageSize = pageSize;
            return this;
        }

        /// <summary>
        /// Builds the leaf controller from <paramref name="provider"/> — the scope this leaf is resolved in.
        /// The serializer is required (a missing one fails fast here, during eager singleton realization);
        /// handler types are activated from the same provider so their dependencies are injected at session scope.
        /// </summary>
        public IToolsController BuildLeaf(IServiceProvider provider)
        {
            var controller = new DefaultToolsController(provider.GetRequiredService<IJson>());
            if (_pageSize.HasValue)
                controller.PageSize = _pageSize.Value;

            foreach (var handler in _handlers)
                controller.AddTool(handler);

            foreach (var handlerType in _handlerTypes)
                controller.AddTool((IToolHandler)ActivatorUtilities.CreateInstance(provider, handlerType));

            return controller;
        }
    }
}
