using System;
using System.Collections.Generic;

namespace McpSdk.Shared
{
    /// <summary>
    /// The frozen resolver produced by <see cref="DiContainer.BuildServiceProvider()"/>. Resolves services
    /// by instance, factory delegate, or reflection-based constructor injection. Every registration is
    /// retained in registration order: <see cref="GetService"/> resolves the last one (last-wins), while
    /// <see cref="GetServices"/> returns them all. Singletons are realized eagerly during construction and
    /// cached per <see cref="ServiceDescriptor"/>, so after the constructor returns this provider is read-only.
    /// </summary>
    /// <remarks>
    /// A provider may be a <em>child</em> of another (see <see cref="CreateChild"/>): it resolves its own
    /// registrations first and delegates anything it does not register to its parent, returning the parent's
    /// singletons <em>by reference</em> (the parent realized them eagerly and is read-only thereafter). A
    /// child realizes only its own singletons at construction time, so it too is read-only afterwards and
    /// many children can be created and resolved concurrently off one shared parent.
    /// </remarks>
    internal sealed class ServiceProvider : IServiceProvider, IDisposable
    {
        private readonly Dictionary<Type, List<ServiceDescriptor>> _descriptors;
        private readonly Dictionary<ServiceDescriptor, object> _singletons = new Dictionary<ServiceDescriptor, object>();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly ServiceProvider _parent;
        private bool _disposed;

        public ServiceProvider(IEnumerable<ServiceDescriptor> descriptors)
            : this(descriptors, null)
        {
        }

        private ServiceProvider(IEnumerable<ServiceDescriptor> descriptors, ServiceProvider parent)
        {
            _parent = parent;
            _descriptors = new Dictionary<Type, List<ServiceDescriptor>>();
            foreach (var descriptor in descriptors)
            {
                // Retain every descriptor per service type, in registration order, instead of overwriting.
                if (!_descriptors.TryGetValue(descriptor.ServiceType, out var list))
                {
                    list = new List<ServiceDescriptor>();
                    _descriptors[descriptor.ServiceType] = list;
                }
                list.Add(descriptor);
            }

            // Eagerly realize every singleton: fail fast on wiring errors, and leave the provider read-only.
            // Each singleton descriptor realizes its own instance (cache is keyed per descriptor), so two
            // singleton registrations of the same service type yield two distinct instances. A child realizes
            // only its OWN singletons here; dependencies it does not register are pulled from the (already
            // realized, read-only) parent, so the parent's singletons are shared by reference, not re-created.
            foreach (var list in _descriptors.Values)
                foreach (var descriptor in list)
                    if (descriptor.Lifetime == ServiceLifetime.Singleton)
                        ResolveDescriptor(descriptor, new List<Type>());
        }

        /// <summary>
        /// Derives a child provider that adds <paramref name="childDescriptors"/> on top of this provider.
        /// The child resolves a type it registers itself; anything else delegates to this (parent) provider,
        /// sharing the parent's singletons by reference. The child eagerly realizes only its own singletons,
        /// so it is read-only after construction and safe to create/resolve concurrently with its siblings.
        /// Used by the HTTP host to give each connection a per-session scope over the shared root.
        /// </summary>
        internal ServiceProvider CreateChild(IEnumerable<ServiceDescriptor> childDescriptors)
        {
            if (childDescriptors == null) throw new ArgumentNullException(nameof(childDescriptors));
            return new ServiceProvider(childDescriptors, this);
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            return Resolve(serviceType, new List<Type>());
        }

        /// <summary>
        /// Resolves every registration for <paramref name="serviceType"/>, in registration order. Returns an
        /// empty sequence when nothing is registered for the type. On a child provider the parent's
        /// registrations come first, then this provider's — so a child overlays (is appended after) its parent.
        /// </summary>
        public IEnumerable<object> GetServices(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            // The provider resolves itself; treat it as a single registration. Each provider (including a
            // child) resolves IServiceProvider to itself, so this is never delegated to the parent.
            if (serviceType == typeof(IServiceProvider))
            {
                yield return this;
                yield break;
            }

            // Parent registrations first, then this provider's: a child is overlaid on top of its parent.
            if (_parent != null)
                foreach (var service in _parent.GetServices(serviceType))
                    yield return service;

            if (!_descriptors.TryGetValue(serviceType, out var list))
                yield break;

            foreach (var descriptor in list)
                yield return ResolveDescriptor(descriptor, new List<Type>());
        }

        private object Resolve(Type serviceType, List<Type> chain)
        {
            // The provider resolves itself, so factory delegates and constructors may depend on IServiceProvider.
            if (serviceType == typeof(IServiceProvider))
                return this;

            if (!_descriptors.TryGetValue(serviceType, out var list))
            {
                // Not registered here. A child delegates to its parent so a type it does not override resolves
                // from the parent — sharing the parent's eagerly realized (read-only) singletons by reference.
                if (_parent != null)
                    return _parent.Resolve(serviceType, chain);

                // IServiceProvider contract: an unregistered top-level request returns null. A missing
                // dependency encountered while activating another service is an error.
                if (chain.Count == 0)
                    return null;
                throw new InvalidOperationException(
                    $"Unable to resolve service for type '{serviceType}' while attempting to activate '{chain[chain.Count - 1]}'.");
            }

            // Last registration wins for single-service resolution.
            return ResolveDescriptor(list[list.Count - 1], chain);
        }

        private object ResolveDescriptor(ServiceDescriptor descriptor, List<Type> chain)
        {
            if (descriptor.Lifetime == ServiceLifetime.Singleton && _singletons.TryGetValue(descriptor, out var cached))
                return cached;

            var serviceType = descriptor.ServiceType;
            if (chain.Contains(serviceType))
            {
                chain.Add(serviceType);
                throw new InvalidOperationException(
                    $"A circular dependency was detected for the service of type '{serviceType}': {string.Join(" -> ", ToNames(chain))}.");
            }

            chain.Add(serviceType);
            var instance = Create(descriptor, chain);
            chain.RemoveAt(chain.Count - 1);

            if (descriptor.Lifetime == ServiceLifetime.Singleton)
            {
                _singletons[descriptor] = instance;
                if (descriptor.ImplementationInstance == null && instance is IDisposable disposable)
                    _disposables.Add(disposable);
            }

            return instance;
        }

        private object Create(ServiceDescriptor descriptor, List<Type> chain)
        {
            if (descriptor.ImplementationInstance != null)
                return descriptor.ImplementationInstance;

            if (descriptor.ImplementationFactory != null)
                return descriptor.ImplementationFactory(this);

            var implementationType = descriptor.ImplementationType;
            var constructor = ActivatorUtilities.SelectConstructor(implementationType, CanResolve);
            var parameters = constructor.GetParameters();
            var arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                arguments[i] = Resolve(parameters[i].ParameterType, chain);

            return constructor.Invoke(arguments);
        }

        /// <summary>
        /// Reports whether this provider (or, for a child, its parent chain) can supply
        /// <paramref name="serviceType"/> — used by <see cref="ActivatorUtilities"/> to test constructor
        /// satisfiability without instantiating anything. <see cref="IServiceProvider"/> is always resolvable
        /// (the provider resolves itself).
        /// </summary>
        internal bool CanResolve(Type serviceType)
        {
            if (serviceType == typeof(IServiceProvider))
                return true;
            if (_descriptors.ContainsKey(serviceType))
                return true;
            return _parent != null && _parent.CanResolve(serviceType);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (var i = _disposables.Count - 1; i >= 0; i--)
                _disposables[i].Dispose();
        }

        private static IEnumerable<string> ToNames(IEnumerable<Type> types)
        {
            foreach (var type in types)
                yield return type.Name;
        }
    }
}
