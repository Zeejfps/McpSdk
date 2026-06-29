using System;
using System.Collections.Generic;
using McpSdk.Protocol;
using McpSdk.Shared;

namespace McpSdk.Server
{
    internal sealed class ToolsBuilder : IToolsBuilder
    {
        private readonly List<IToolHandler> _handlers = new List<IToolHandler>();
        private readonly List<Type> _handlerTypes = new List<Type>();
        private int? _pageSize;

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
