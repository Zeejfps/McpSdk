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
        /// Returns the pre-built instance from the most recent <see cref="ServiceLifetime.Singleton"/>
        /// registration of <typeparamref name="TService"/> that was added as an instance (via
        /// <c>AddSingleton(instance)</c>), or <c>null</c> if there is none. This inspects the pending
        /// registrations directly — it neither calls <see cref="BuildServiceProvider()"/> nor realizes any
        /// service — so a registration-time helper (such as <c>ConfigureInfo</c>) can mutate an already-seeded
        /// options singleton in place without triggering resolution of unrelated, not-yet-wired services.
        /// </summary>
        public TService GetRegisteredInstance<TService>() where TService : class
        {
            for (var i = _descriptors.Count - 1; i >= 0; i--)
            {
                var descriptor = _descriptors[i];
                if (descriptor.ServiceType == typeof(TService) && descriptor.ImplementationInstance is TService instance)
                    return instance;
            }
            return null;
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

        /// <summary>
        /// Freezes the current registrations as a <em>child</em> scope of <paramref name="parent"/>: this
        /// container's services are resolved from the child, anything else delegates to the parent (sharing
        /// the parent's singletons by reference), and <c>GetServices</c> overlays this container's
        /// registrations after the parent's. The child realizes only its own singletons here, so it is
        /// read-only afterwards and safe to create concurrently with sibling children. This is how a fresh
        /// <see cref="DiContainer"/> becomes a per-session <c>session.Context</c> layered over the host root.
        /// </summary>
        public IServiceProvider BuildServiceProvider(IServiceProvider parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (!(parent is ServiceProvider serviceProvider))
                throw new ArgumentException(
                    $"The parent provider must be a provider produced by {nameof(DiContainer)}.{nameof(BuildServiceProvider)}.",
                    nameof(parent));
            return serviceProvider.CreateChild(_descriptors);
        }
    }
}
