using System;
using System.Reflection;

namespace McpSdk.Shared
{
    /// <summary>The lifetime of a service registered in an <see cref="IContext"/>.</summary>
    public enum ServiceLifetime
    {
        /// <summary>A single instance is created per <see cref="System.IServiceProvider"/> and reused.</summary>
        Singleton,

        /// <summary>A new instance is created every time the service is resolved.</summary>
        Transient,
    }

    /// <summary>
    /// Describes a single service registration: the service type, its lifetime, and how an instance is
    /// produced (a pre-built instance, a factory delegate, or an implementation type activated via
    /// constructor injection). Mirrors Microsoft.Extensions.DependencyInjection's <c>ServiceDescriptor</c>.
    /// </summary>
    public sealed class ServiceDescriptor
    {
        /// <summary>The type the service is registered and resolved as.</summary>
        public Type ServiceType { get; }

        /// <summary>The lifetime of the service.</summary>
        public ServiceLifetime Lifetime { get; }

        /// <summary>The implementation type activated via constructor injection, or <c>null</c>.</summary>
        public Type ImplementationType { get; }

        /// <summary>A pre-built instance to hand back, or <c>null</c>.</summary>
        public object ImplementationInstance { get; }

        /// <summary>A factory that produces the instance, or <c>null</c>.</summary>
        public Func<IServiceProvider, object> ImplementationFactory { get; }

        private ServiceDescriptor(
            Type serviceType,
            ServiceLifetime lifetime,
            Type implementationType,
            object implementationInstance,
            Func<IServiceProvider, object> implementationFactory)
        {
            ServiceType = serviceType;
            Lifetime = lifetime;
            ImplementationType = implementationType;
            ImplementationInstance = implementationInstance;
            ImplementationFactory = implementationFactory;
        }

        /// <summary>A singleton registration backed by a pre-built instance.</summary>
        public static ServiceDescriptor Singleton(Type serviceType, object instance)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return new ServiceDescriptor(serviceType, ServiceLifetime.Singleton, null, instance, null);
        }

        /// <summary>A singleton registration activated from <paramref name="implementationType"/>.</summary>
        public static ServiceDescriptor Singleton(Type serviceType, Type implementationType)
            => Typed(serviceType, implementationType, ServiceLifetime.Singleton);

        /// <summary>A singleton registration produced by a factory delegate.</summary>
        public static ServiceDescriptor Singleton(Type serviceType, Func<IServiceProvider, object> factory)
            => FactoryBased(serviceType, factory, ServiceLifetime.Singleton);

        /// <summary>A transient registration activated from <paramref name="implementationType"/>.</summary>
        public static ServiceDescriptor Transient(Type serviceType, Type implementationType)
            => Typed(serviceType, implementationType, ServiceLifetime.Transient);

        /// <summary>A transient registration produced by a factory delegate.</summary>
        public static ServiceDescriptor Transient(Type serviceType, Func<IServiceProvider, object> factory)
            => FactoryBased(serviceType, factory, ServiceLifetime.Transient);

        private static ServiceDescriptor Typed(Type serviceType, Type implementationType, ServiceLifetime lifetime)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (implementationType == null) throw new ArgumentNullException(nameof(implementationType));

            var info = implementationType.GetTypeInfo();
            if (info.IsAbstract || info.IsInterface)
                throw new ArgumentException(
                    $"Cannot instantiate implementation type '{implementationType}' for service type '{serviceType}'.",
                    nameof(implementationType));
            if (info.ContainsGenericParameters)
                throw new ArgumentException(
                    $"Open generic implementation type '{implementationType}' is not supported.",
                    nameof(implementationType));

            return new ServiceDescriptor(serviceType, lifetime, implementationType, null, null);
        }

        private static ServiceDescriptor FactoryBased(Type serviceType, Func<IServiceProvider, object> factory, ServiceLifetime lifetime)
        {
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return new ServiceDescriptor(serviceType, lifetime, null, null, factory);
        }
    }
}
