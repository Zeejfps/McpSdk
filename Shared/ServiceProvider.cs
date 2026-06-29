using System;
using System.Collections.Generic;

namespace McpSdk.Shared
{
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
                if (!_descriptors.TryGetValue(descriptor.ServiceType, out var list))
                {
                    list = new List<ServiceDescriptor>();
                    _descriptors[descriptor.ServiceType] = list;
                }
                list.Add(descriptor);
            }

            foreach (var list in _descriptors.Values)
                foreach (var descriptor in list)
                    if (descriptor.Lifetime == ServiceLifetime.Singleton)
                        ResolveDescriptor(descriptor, new List<Type>());
        }

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

        public IEnumerable<object> GetServices(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            if (serviceType == typeof(IServiceProvider))
            {
                yield return this;
                yield break;
            }

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
            if (serviceType == typeof(IServiceProvider))
                return this;

            if (!_descriptors.TryGetValue(serviceType, out var list))
            {
                if (_parent != null)
                    return _parent.Resolve(serviceType, chain);

                if (chain.Count == 0)
                    return null;
                throw new InvalidOperationException(
                    $"Unable to resolve service for type '{serviceType}' while attempting to activate '{chain[chain.Count - 1]}'.");
            }

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
