using System;
using System.Collections.Generic;
using System.Reflection;

namespace McpSdk.Shared
{
    /// <summary>
    /// The frozen resolver produced by <see cref="DiContainer.BuildServiceProvider"/>. Resolves services
    /// by instance, factory delegate, or reflection-based constructor injection. Singletons are realized
    /// eagerly during construction and cached, so after the constructor returns this provider is read-only.
    /// </summary>
    internal sealed class ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, ServiceDescriptor> _descriptors;
        private readonly Dictionary<Type, object> _singletons = new Dictionary<Type, object>();

        public ServiceProvider(IEnumerable<ServiceDescriptor> descriptors)
        {
            _descriptors = new Dictionary<Type, ServiceDescriptor>();
            foreach (var descriptor in descriptors)
                _descriptors[descriptor.ServiceType] = descriptor; // last registration wins

            // The provider resolves itself, so factory delegates and constructors may depend on IServiceProvider.
            _singletons[typeof(IServiceProvider)] = this;

            // Eagerly realize every singleton: fail fast on wiring errors, and leave the provider read-only.
            foreach (var descriptor in _descriptors.Values)
            {
                if (descriptor.Lifetime == ServiceLifetime.Singleton)
                    Resolve(descriptor.ServiceType, new List<Type>());
            }
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            return Resolve(serviceType, new List<Type>());
        }

        private object Resolve(Type serviceType, List<Type> chain)
        {
            if (_singletons.TryGetValue(serviceType, out var cached))
                return cached;

            if (!_descriptors.TryGetValue(serviceType, out var descriptor))
            {
                // IServiceProvider contract: an unregistered top-level request returns null. A missing
                // dependency encountered while activating another service is an error.
                if (chain.Count == 0)
                    return null;
                throw new InvalidOperationException(
                    $"Unable to resolve service for type '{serviceType}' while attempting to activate '{chain[chain.Count - 1]}'.");
            }

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
                _singletons[serviceType] = instance;

            return instance;
        }

        private object Create(ServiceDescriptor descriptor, List<Type> chain)
        {
            if (descriptor.ImplementationInstance != null)
                return descriptor.ImplementationInstance;

            if (descriptor.ImplementationFactory != null)
                return descriptor.ImplementationFactory(this);

            var implementationType = descriptor.ImplementationType;
            var constructor = SelectConstructor(implementationType);
            var parameters = constructor.GetParameters();
            var arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
                arguments[i] = Resolve(parameters[i].ParameterType, chain);

            return constructor.Invoke(arguments);
        }

        private ConstructorInfo SelectConstructor(Type implementationType)
        {
            var constructors = implementationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            if (constructors.Length == 0)
                throw new InvalidOperationException(
                    $"A suitable constructor for type '{implementationType}' could not be located. Ensure the type has a public constructor.");

            // Pick the greediest constructor whose parameters are all resolvable. GetConstructors() has no
            // guaranteed order, so an equal-arity tie is reported as ambiguous rather than picked at random.
            ConstructorInfo best = null;
            var bestParameterCount = -1;
            var ambiguous = false;

            foreach (var constructor in constructors)
            {
                if (!IsSatisfiable(constructor))
                    continue;

                var parameterCount = constructor.GetParameters().Length;
                if (parameterCount > bestParameterCount)
                {
                    best = constructor;
                    bestParameterCount = parameterCount;
                    ambiguous = false;
                }
                else if (parameterCount == bestParameterCount)
                {
                    ambiguous = true;
                }
            }

            if (best == null)
                throw new InvalidOperationException(
                    $"A suitable constructor for type '{implementationType}' could not be located. " +
                    "Ensure all of its constructor parameters are registered services.");

            if (ambiguous)
                throw new InvalidOperationException(
                    $"Multiple constructors accepting all given argument types have been found in type '{implementationType}'. " +
                    "There should only be one applicable constructor.");

            return best;
        }

        private bool IsSatisfiable(ConstructorInfo constructor)
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (parameter.ParameterType == typeof(IServiceProvider))
                    continue;
                if (!_descriptors.ContainsKey(parameter.ParameterType))
                    return false;
            }
            return true;
        }

        private static IEnumerable<string> ToNames(IEnumerable<Type> types)
        {
            foreach (var type in types)
                yield return type.Name;
        }
    }
}
