namespace McpSdk.Shared
{
    /// <summary>
    /// Holds a configured options instance, mirroring Microsoft.Extensions.Options' <c>IOptions&lt;T&gt;</c>.
    /// Register and configure the value with <see cref="OptionsContextExtensions.Configure{TOptions}"/>,
    /// then depend on <see cref="IOptions{TOptions}"/> (read <see cref="Value"/>) via constructor injection.
    /// </summary>
    public interface IOptions<out TOptions> where TOptions : class
    {
        /// <summary>The configured options instance.</summary>
        TOptions Value { get; }
    }
}
