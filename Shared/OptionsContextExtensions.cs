using System;

namespace McpSdk.Shared
{
    /// <summary>Options registration helpers for <see cref="IContext"/>, modeled on Microsoft.Extensions.Options.</summary>
    public static class OptionsContextExtensions
    {
        /// <summary>
        /// Creates a <typeparamref name="TOptions"/>, applies <paramref name="configure"/> to it, and registers
        /// it as <see cref="IOptions{TOptions}"/>. Note: unlike Microsoft.Extensions.Options, repeated calls
        /// for the same type do not compose — the last registration wins.
        /// </summary>
        public static IContext Configure<TOptions>(this IContext context, Action<TOptions> configure)
            where TOptions : class, new()
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            var options = new TOptions();
            configure(options);
            return context.AddSingleton<IOptions<TOptions>>(new Options<TOptions>(options));
        }

        private sealed class Options<TOptions> : IOptions<TOptions> where TOptions : class
        {
            public TOptions Value { get; }
            public Options(TOptions value) { Value = value; }
        }
    }
}
