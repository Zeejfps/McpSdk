#nullable disable
using System;
using System.Threading.Tasks;
using McpSdk.Shared;

namespace McpSdk.Server.Tests.Conformance
{
    /// <summary>
    /// Direct sanity checks for the <see cref="DiContainer"/> / <see cref="IContext"/> registration and
    /// resolution behaviour (instance / factory / reflection constructor injection, lifetimes,
    /// last-registration-wins, self-resolution, and the error paths the transport happy path never hits:
    /// missing service, circular dependency, ambiguous constructor).
    /// </summary>
    public static partial class ConformanceTests
    {
        private static Task InstanceResolutionReturnsSameInstance()
        {
            var instance = new Alpha();
            var container = new DiContainer();
            container.AddSingleton<IAlpha>(instance);
            var provider = container.BuildServiceProvider();

            Assert(ReferenceEquals(provider.GetRequiredService<IAlpha>(), instance),
                "an instance registration resolves the same object");
            return Task.CompletedTask;
        }

        private static Task ReflectionConstructorInjection()
        {
            var container = new DiContainer();
            container.AddSingleton<IAlpha, Alpha>();
            container.AddSingleton<IBeta, Beta>();
            var provider = container.BuildServiceProvider();

            var beta = provider.GetRequiredService<IBeta>();
            Assert(beta.Alpha != null, "constructor parameter IAlpha was injected into Beta");
            Assert(ReferenceEquals(beta.Alpha, provider.GetRequiredService<IAlpha>()),
                "the injected IAlpha is the registered singleton");
            return Task.CompletedTask;
        }

        private static Task FactoryRegistrationUsesProvider()
        {
            var container = new DiContainer();
            container.AddSingleton<IAlpha, Alpha>();
            container.AddSingleton<IBeta>(sp => new Beta(sp.GetRequiredService<IAlpha>()));
            var provider = container.BuildServiceProvider();

            Assert(provider.GetRequiredService<IBeta>().Alpha != null,
                "a factory registration resolves its dependencies from the provider");
            return Task.CompletedTask;
        }

        private static Task SingletonIsCachedTransientIsNot()
        {
            var singletonContainer = new DiContainer();
            singletonContainer.AddSingleton<Counter, Counter>();
            var singletonProvider = singletonContainer.BuildServiceProvider();
            Assert(ReferenceEquals(singletonProvider.GetRequiredService<Counter>(),
                    singletonProvider.GetRequiredService<Counter>()),
                "a singleton resolves to the same instance every time");

            var transientContainer = new DiContainer();
            transientContainer.AddTransient<Counter, Counter>();
            var transientProvider = transientContainer.BuildServiceProvider();
            Assert(!ReferenceEquals(transientProvider.GetRequiredService<Counter>(),
                    transientProvider.GetRequiredService<Counter>()),
                "a transient resolves to a fresh instance every time");
            return Task.CompletedTask;
        }

        private static Task LastRegistrationWins()
        {
            var first = new Alpha();
            var second = new Alpha();
            var container = new DiContainer();
            container.AddSingleton<IAlpha>(first);
            container.AddSingleton<IAlpha>(second);
            var provider = container.BuildServiceProvider();

            Assert(ReferenceEquals(provider.GetRequiredService<IAlpha>(), second),
                "the last registration for a service type wins");
            return Task.CompletedTask;
        }

        private static Task ProviderResolvesItself()
        {
            var provider = new DiContainer().BuildServiceProvider();
            Assert(ReferenceEquals(provider.GetRequiredService<IServiceProvider>(), provider),
                "the provider resolves IServiceProvider to itself");
            return Task.CompletedTask;
        }

        private static Task GetServiceReturnsNullGetRequiredThrows()
        {
            var provider = new DiContainer().BuildServiceProvider();
            Assert(provider.GetService<IAlpha>() == null,
                "GetService returns null for an unregistered top-level type");

            Assert(Throws<InvalidOperationException>(() => provider.GetRequiredService<IAlpha>(), "No service for type"),
                "GetRequiredService throws for an unregistered type");
            return Task.CompletedTask;
        }

        private static Task CircularDependencyThrows()
        {
            var container = new DiContainer();
            container.AddSingleton<ICycleA, CycleA>();
            container.AddSingleton<ICycleB, CycleB>();

            Assert(Throws<InvalidOperationException>(() => container.BuildServiceProvider(), "circular dependency"),
                "a circular dependency is detected");
            return Task.CompletedTask;
        }

        private static Task AmbiguousConstructorThrows()
        {
            var container = new DiContainer();
            container.AddSingleton<IAlpha, Alpha>();
            container.AddSingleton<IBeta, Beta>();
            container.AddSingleton<IAmbiguous, Ambiguous>();

            Assert(Throws<InvalidOperationException>(() => container.BuildServiceProvider(), "Multiple constructors"),
                "an equal-arity constructor tie is reported as ambiguous");
            return Task.CompletedTask;
        }

        private static Task OptionsConfigureAndResolve()
        {
            var container = new DiContainer();
            container.Configure<SampleOptions>(o => o.Name = "configured");
            var provider = container.BuildServiceProvider();

            Assert(provider.GetRequiredService<IOptions<SampleOptions>>().Value.Name == "configured",
                "Configure<T> builds options resolvable via IOptions<T>");
            return Task.CompletedTask;
        }

        private static bool Throws<TException>(Action action, string messageContains) where TException : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (TException ex)
            {
                return ex.Message.IndexOf(messageContains, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch
            {
                return false;
            }
        }

        // -- Fixtures ------------------------------------------------------------------------

        private interface IAlpha { }

        private sealed class Alpha : IAlpha { }

        private interface IBeta { IAlpha Alpha { get; } }

        private sealed class Beta : IBeta
        {
            public IAlpha Alpha { get; }
            public Beta(IAlpha alpha) { Alpha = alpha; }
        }

        private sealed class Counter { }

        private sealed class SampleOptions
        {
            public string Name { get; set; }
        }

        private interface ICycleA { }
        private interface ICycleB { }

        private sealed class CycleA : ICycleA
        {
            public CycleA(ICycleB b) { _ = b; }
        }

        private sealed class CycleB : ICycleB
        {
            public CycleB(ICycleA a) { _ = a; }
        }

        private interface IAmbiguous { }

        private sealed class Ambiguous : IAmbiguous
        {
            public Ambiguous(IAlpha alpha) { _ = alpha; }
            public Ambiguous(IBeta beta) { _ = beta; }
        }
    }
}
