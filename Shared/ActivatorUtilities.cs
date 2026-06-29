using System;
using System.Reflection;

namespace McpSdk.Shared
{
    /// <summary>
    /// Activates a concrete type that is <em>not</em> registered in a provider, pulling its constructor
    /// parameters from an <see cref="IServiceProvider"/>. Mirrors
    /// Microsoft.Extensions.DependencyInjection's <c>ActivatorUtilities</c>: it selects the same
    /// greediest-satisfiable public constructor the provider uses for registered types (see
    /// <see cref="SelectConstructor"/>) — treating a parameter of type <see cref="IServiceProvider"/> as the
    /// provider itself — so an unregistered type activated here follows identical wiring rules to a
    /// registered service. Used by <c>AddTool&lt;THandler&gt;()</c> to construct an unregistered handler at
    /// session scope.
    /// </summary>
    public static class ActivatorUtilities
    {
        /// <summary>
        /// Creates an instance of <paramref name="type"/> (which need not be registered) by selecting its
        /// greediest public constructor whose parameters the <paramref name="provider"/> can supply, then
        /// resolving each parameter from the provider. A parameter of type <see cref="IServiceProvider"/>
        /// receives the provider itself. Throws if no constructor is satisfiable, or if an equal-arity tie
        /// makes the choice ambiguous — the same rules the provider applies to a registered type.
        /// </summary>
        public static object CreateInstance(IServiceProvider provider, Type type)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (type == null) throw new ArgumentNullException(nameof(type));

            var constructor = SelectConstructor(type, serviceType => CanResolve(provider, serviceType));
            var parameters = constructor.GetParameters();
            var arguments = new object[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;
                arguments[i] = parameterType == typeof(IServiceProvider)
                    ? provider
                    : provider.GetService(parameterType);
            }

            return constructor.Invoke(arguments);
        }

        /// <summary>Generic convenience overload of <see cref="CreateInstance(IServiceProvider, Type)"/>.</summary>
        public static T CreateInstance<T>(IServiceProvider provider)
            => (T)CreateInstance(provider, typeof(T));

        /// <summary>
        /// Selects the greediest public instance constructor of <paramref name="implementationType"/> whose
        /// parameters are all satisfiable, where <paramref name="canResolve"/> reports whether a given
        /// parameter type can be supplied (a parameter of type <see cref="IServiceProvider"/> is always
        /// satisfiable). This is the single selection rule shared by <see cref="ServiceProvider"/> (for
        /// registered types) and <see cref="CreateInstance(IServiceProvider, Type)"/> (for unregistered
        /// types), so the two can never diverge: <c>GetConstructors()</c> has no guaranteed order, so an
        /// equal-arity tie is reported as ambiguous rather than picked at random, and a type with no
        /// satisfiable constructor throws.
        /// </summary>
        internal static ConstructorInfo SelectConstructor(Type implementationType, Func<Type, bool> canResolve)
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
                if (!IsSatisfiable(constructor, canResolve))
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

        private static bool IsSatisfiable(ConstructorInfo constructor, Func<Type, bool> canResolve)
        {
            foreach (var parameter in constructor.GetParameters())
            {
                if (parameter.ParameterType == typeof(IServiceProvider))
                    continue;
                if (!canResolve(parameter.ParameterType))
                    return false;
            }
            return true;
        }

        private static bool CanResolve(IServiceProvider provider, Type serviceType)
        {
            // Our own provider answers from its descriptor chain without instantiating anything; a foreign
            // IServiceProvider is probed by resolving — a registered service yields a non-null instance.
            if (provider is ServiceProvider serviceProvider)
                return serviceProvider.CanResolve(serviceType);
            return provider.GetService(serviceType) != null;
        }
    }
}
