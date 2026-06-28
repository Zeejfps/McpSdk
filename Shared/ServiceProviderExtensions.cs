using System;

namespace McpSdk.Shared
{
    /// <summary>
    /// Typed resolution helpers for <see cref="IServiceProvider"/>, mirroring
    /// Microsoft.Extensions.DependencyInjection's <c>GetService&lt;T&gt;</c> / <c>GetRequiredService&lt;T&gt;</c>.
    /// </summary>
    public static class ServiceProviderExtensions
    {
        /// <summary>Resolves <typeparamref name="T"/>, or returns its default if not registered.</summary>
        public static T GetService<T>(this IServiceProvider provider)
        {
            var service = provider.GetService(typeof(T));
            return service == null ? default(T) : (T)service;
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
