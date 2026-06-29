using System;

namespace McpSdk.Shared
{
    /// <summary>
    /// Registration helpers for <see cref="IContext"/>, mirroring Microsoft.Extensions.DependencyInjection's
    /// <c>AddSingleton</c> / <c>AddTransient</c> family. Each method returns the same <see cref="IContext"/>
    /// so calls can be chained.
    /// </summary>
    public static class ContextRegistrationExtensions
    {
        /// <summary>Registers a pre-built singleton instance for <typeparamref name="TService"/>.</summary>
        public static IContext AddSingleton<TService>(this IContext context, TService instance)
            => context.Add(ServiceDescriptor.Singleton(typeof(TService), instance));

        /// <summary>
        /// Registers <typeparamref name="TImplementation"/> as a singleton for <typeparamref name="TService"/>,
        /// activated via constructor injection.
        /// </summary>
        public static IContext AddSingleton<TService, TImplementation>(this IContext context)
            where TImplementation : TService
            => context.Add(ServiceDescriptor.Singleton(typeof(TService), typeof(TImplementation)));

        /// <summary>Registers a singleton for <typeparamref name="TService"/> produced by a factory.</summary>
        public static IContext AddSingleton<TService>(this IContext context, Func<IServiceProvider, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return context.Add(ServiceDescriptor.Singleton(typeof(TService), sp => factory(sp)));
        }

        /// <summary>
        /// Registers a singleton factory for <typeparamref name="TService"/> only if no registration for
        /// <typeparamref name="TService"/> exists yet (mirrors Microsoft.Extensions.DependencyInjection's
        /// <c>TryAddSingleton</c>). Returns the same <see cref="IContext"/> so calls can be chained.
        /// </summary>
        public static IContext TryAddSingleton<TService>(this IContext context, Func<IServiceProvider, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            context.TryAdd(ServiceDescriptor.Singleton(typeof(TService), sp => factory(sp)));
            return context;
        }

        /// <summary>
        /// Registers <typeparamref name="TImplementation"/> as a transient for <typeparamref name="TService"/>,
        /// activated via constructor injection on each resolve.
        /// </summary>
        public static IContext AddTransient<TService, TImplementation>(this IContext context)
            where TImplementation : TService
            => context.Add(ServiceDescriptor.Transient(typeof(TService), typeof(TImplementation)));

        /// <summary>Registers a transient for <typeparamref name="TService"/> produced by a factory.</summary>
        public static IContext AddTransient<TService>(this IContext context, Func<IServiceProvider, TService> factory)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            return context.Add(ServiceDescriptor.Transient(typeof(TService), sp => factory(sp)));
        }
    }
}
