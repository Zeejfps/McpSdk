using System;
using System.Collections.Generic;
using System.Linq;

namespace McpSdk.Shared
{
    /// <summary>
    /// Typed resolution helpers for <see cref="IServiceProvider"/>, mirroring
    /// Microsoft.Extensions.DependencyInjection's <c>GetService&lt;T&gt;</c> / <c>GetRequiredService&lt;T&gt;</c> /
    /// <c>GetServices&lt;T&gt;</c>.
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>Resolves <typeparamref name="T"/>, or returns its default if not registered.</summary>
        public static T GetService<T>(this IServiceProvider provider)
        {
            var service = provider.GetService(typeof(T));
            return service == null ? default(T) : (T)service;
        }

        /// <summary>Resolves every registration for <typeparamref name="T"/>, in registration order.</summary>
        public static IEnumerable<T> GetServices<T>(this IServiceProvider provider)
            => provider.GetServices(typeof(T)).Cast<T>();

        /// <summary>Resolves every registration for <paramref name="serviceType"/>, in registration order.</summary>
        public static IEnumerable<object> GetServices(this IServiceProvider provider, Type serviceType)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

            if (provider is ServiceProvider serviceProvider)
                return serviceProvider.GetServices(serviceType);

            // Fallback for foreign IServiceProvider implementations: surface the single service, if any.
            var service = provider.GetService(serviceType);
            return service == null ? Enumerable.Empty<object>() : new[] { service };
        }

        /// <summary>Resolves <typeparamref name="T"/>, throwing if it is not registered.</summary>
        public static T GetRequiredService<T>(this IServiceProvider provider)
            => (T)provider.GetRequiredService(typeof(T));

        /// <summary>Resolves <paramref name="serviceType"/>, throwing if it is not registered.</summary>
        public static object GetRequiredService(this IServiceProvider provider, Type serviceType)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            var service = provider.GetService(serviceType);
            if (service == null)
                throw new InvalidOperationException($"No service for type '{serviceType}' has been registered.");
            return service;
        }
    }
}
