using System;

namespace McpSdk.Shared
{
    /// <summary>
    /// Helper for the "configure a singleton the builder ctor already seeded" pattern shared by the server's
    /// and client's <c>ConfigureInfo</c>. It mutates the registered <typeparamref name="TOptions"/> instance
    /// in place — found via <see cref="DiContainer.GetRegisteredInstance{TService}"/> without building or
    /// resolving anything — so the value the relevant factory reads at resolve time picks the change up.
    /// </summary>
    public static class SeededOptionsContextExtensions
    {
        /// <summary>
        /// Applies <paramref name="configure"/> to the most recently registered <typeparamref name="TOptions"/>
        /// instance singleton on <paramref name="context"/>. Throws <see cref="ArgumentException"/> if
        /// <paramref name="context"/> is not a <see cref="DiContainer"/> (the only registration surface that
        /// can be inspected without resolving), or <see cref="InvalidOperationException"/> with
        /// <paramref name="missingInstanceMessage"/> if no such instance was seeded.
        /// </summary>
        public static IContext ConfigureSeededOptions<TOptions>(
            this IContext context, Action<TOptions> configure, string missingInstanceMessage)
            where TOptions : class
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            if (!(context is DiContainer container))
                throw new ArgumentException(
                    $"This call requires the builder's Context produced by a {nameof(DiContainer)}.",
                    nameof(context));

            var options = container.GetRegisteredInstance<TOptions>();
            if (options == null)
                throw new InvalidOperationException(missingInstanceMessage);

            configure(options);
            return context;
        }
    }
}
