using System;
using System.Collections.Generic;

namespace McpSdk.Shared
{
    /// <summary>
    /// A lightweight dependency-injection container that implements <see cref="IContext"/> — the
    /// registration surface modeled on Microsoft.Extensions.DependencyInjection's <c>IServiceCollection</c>.
    /// Call <see cref="BuildServiceProvider"/> to freeze the registrations and obtain a resolver.
    /// </summary>
    public sealed class DiContainer : IContext
    {
        private readonly List<ServiceDescriptor> _descriptors = new List<ServiceDescriptor>();

        /// <inheritdoc />
        public IContext Add(ServiceDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            _descriptors.Add(descriptor);
            return this;
        }

        /// <summary>
        /// Freezes the current registrations and returns an <see cref="IServiceProvider"/> that resolves
        /// them. Singletons are realized eagerly so wiring errors surface here rather than at first use,
        /// and the resulting provider is read-only (safe for concurrent resolution).
        /// </summary>
        public IServiceProvider BuildServiceProvider()
        {
            return new ServiceProvider(_descriptors);
        }
    }
}
