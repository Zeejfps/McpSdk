#nullable disable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using McpSdk.Shared;

namespace McpSdk.Server.Tests
{
    /// <summary>
    /// Direct sanity checks for the <see cref="DiContainer"/> / <see cref="IContext"/> registration and
    /// resolution behaviour (instance / factory / reflection constructor injection, lifetimes,
    /// last-registration-wins, self-resolution, and the error paths the transport happy path never hits:
    /// missing service, circular dependency, ambiguous constructor). The <see cref="OptionsConfigureAndResolve"/>
    /// body also exercises the <c>Configure&lt;T&gt;</c> / <see cref="IOptions{T}"/> options infrastructure.
    /// </summary>
    public sealed class DiContainerTests : ConformanceSuite
    {
        public DiContainerTests(TestReport report) : base(report) { }

        public override string Title => "DI Container";

        public override async Task Run()
        {
            await Test("an instance registration resolves the same object", InstanceResolutionReturnsSameInstance);
            await Test("reflection constructor injection wires a registered dependency", ReflectionConstructorInjection);
            await Test("a factory registration resolves its dependencies from the provider", FactoryRegistrationUsesProvider);
            await Test("a singleton is cached but a transient is not", SingletonIsCachedTransientIsNot);
            await Test("the last registration for a service type wins", LastRegistrationWins);
            await Test("GetServices returns every registration in order while GetService returns the last", MultiRegistrationGetServicesReturnsAllInOrder);
            await Test("the singleton cache is keyed per descriptor", SingletonCacheIsPerDescriptor);
            await Test("the provider resolves IServiceProvider to itself", ProviderResolvesItself);
            await Test("GetService returns null while GetRequiredService throws", GetServiceReturnsNullGetRequiredThrows);
            await Test("a circular dependency is detected", CircularDependencyThrows);
            await Test("an equal-arity constructor tie is reported as ambiguous", AmbiguousConstructorThrows);
            await Test("Configure<T> builds options resolvable via IOptions<T>", OptionsConfigureAndResolve);
            await Test("a child override wins over the parent's registration", ChildOverrideWinsOverParent);
            await Test("a parent singleton is shared into the child by reference", ParentSingletonSharedByReference);
            await Test("GetServices on a child overlays its registrations after the parent's", ChildGetServicesOverlaysAfterParent);
            await Test("sibling children do not see each other's child-only services", SiblingChildrenAreIsolated);
            await Test("concurrent child creation and resolution is safe", ConcurrentChildCreationIsSafe);
            await Test("ActivatorUtilities activates an unregistered type injecting a dependency and the provider", ActivatorCreatesUnregisteredTypeWithDependencyAndProvider);
            await Test("ActivatorUtilities throws when an unregistered type needs a missing dependency", ActivatorThrowsWhenDependencyMissing);
            await Test("ActivatorUtilities throws on an equal-arity ambiguous constructor", ActivatorThrowsOnAmbiguousConstructor);
        }

        private Task InstanceResolutionReturnsSameInstance()
        {
            var instance = new Alpha();
            var container = new DiContainer();
            container.AddSingleton<IAlpha>(instance);
            var provider = container.BuildServiceProvider();

            Assert(ReferenceEquals(provider.GetRequiredService<IAlpha>(), instance),
                "an instance registration resolves the same object");
            return Task.CompletedTask;
        }

        private Task ReflectionConstructorInjection()
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

        private Task FactoryRegistrationUsesProvider()
        {
            var container = new DiContainer();
            container.AddSingleton<IAlpha, Alpha>();
            container.AddSingleton<IBeta>(sp => new Beta(sp.GetRequiredService<IAlpha>()));
            var provider = container.BuildServiceProvider();

            Assert(provider.GetRequiredService<IBeta>().Alpha != null,
                "a factory registration resolves its dependencies from the provider");
            return Task.CompletedTask;
        }

        private Task SingletonIsCachedTransientIsNot()
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

        private Task LastRegistrationWins()
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

        private Task MultiRegistrationGetServicesReturnsAllInOrder()
        {
            var first = new Foo();
            var second = new OtherFoo();
            var container = new DiContainer();
            container.AddSingleton<IFoo>(first);
            container.AddSingleton<IFoo>(second);
            var provider = container.BuildServiceProvider();

            Assert(ReferenceEquals(provider.GetRequiredService<IFoo>(), second),
                "GetService resolves the last of multiple registrations");

            var all = new List<IFoo>(provider.GetServices<IFoo>());
            Assert(all.Count == 2, "GetServices returns both registrations (count == 2)");
            Assert(ReferenceEquals(all[0], first), "GetServices preserves order: the first-registered comes first");
            Assert(ReferenceEquals(all[1], second), "GetServices preserves order: the second-registered comes second");
            return Task.CompletedTask;
        }

        private Task SingletonCacheIsPerDescriptor()
        {
            var container = new DiContainer();
            container.AddSingleton<IFoo, Foo>();
            container.AddSingleton<IFoo, Foo>(); // two distinct descriptors for the same service type
            var provider = container.BuildServiceProvider();

            var all = new List<IFoo>(provider.GetServices<IFoo>());
            Assert(all.Count == 2, "two singleton descriptors of one service type yield two registrations");
            Assert(!ReferenceEquals(all[0], all[1]),
                "two distinct singleton descriptors realize two instances (per-descriptor cache)");

            Assert(ReferenceEquals(provider.GetRequiredService<IFoo>(), provider.GetRequiredService<IFoo>()),
                "one singleton descriptor resolved twice returns the same instance");
            return Task.CompletedTask;
        }

        private Task ProviderResolvesItself()
        {
            var provider = new DiContainer().BuildServiceProvider();
            Assert(ReferenceEquals(provider.GetRequiredService<IServiceProvider>(), provider),
                "the provider resolves IServiceProvider to itself");
            return Task.CompletedTask;
        }

        private Task GetServiceReturnsNullGetRequiredThrows()
        {
            var provider = new DiContainer().BuildServiceProvider();
            Assert(provider.GetService<IAlpha>() == null,
                "GetService returns null for an unregistered top-level type");

            Throws<InvalidOperationException>(() => provider.GetRequiredService<IAlpha>(),
                "GetRequiredService throws for an unregistered type", "No service for type");
            return Task.CompletedTask;
        }

        private Task CircularDependencyThrows()
        {
            var container = new DiContainer();
            container.AddSingleton<ICycleA, CycleA>();
            container.AddSingleton<ICycleB, CycleB>();

            Throws<InvalidOperationException>(() => container.BuildServiceProvider(),
                "a circular dependency is detected", "circular dependency");
            return Task.CompletedTask;
        }

        private Task AmbiguousConstructorThrows()
        {
            var container = new DiContainer();
            container.AddSingleton<IAlpha, Alpha>();
            container.AddSingleton<IBeta, Beta>();
            container.AddSingleton<IAmbiguous, Ambiguous>();

            Throws<InvalidOperationException>(() => container.BuildServiceProvider(),
                "an equal-arity constructor tie is reported as ambiguous", "Multiple constructors");
            return Task.CompletedTask;
        }

        private Task OptionsConfigureAndResolve()
        {
            var container = new DiContainer();
            container.Configure<SampleOptions>(o => o.Name = "configured");
            var provider = container.BuildServiceProvider();

            Assert(provider.GetRequiredService<IOptions<SampleOptions>>().Value.Name == "configured",
                "Configure<T> builds options resolvable via IOptions<T>");
            return Task.CompletedTask;
        }

        private Task ChildOverrideWinsOverParent()
        {
            var rootInstance = new Alpha();
            var childInstance = new Alpha();

            var rootContainer = new DiContainer();
            rootContainer.AddSingleton<IAlpha>(rootInstance);
            var root = rootContainer.BuildServiceProvider();

            var childContainer = new DiContainer();
            childContainer.AddSingleton<IAlpha>(childInstance);
            var child = childContainer.BuildServiceProvider(root);

            Assert(ReferenceEquals(child.GetRequiredService<IAlpha>(), childInstance),
                "a service registered in both root and child resolves to the child's via the child provider");
            Assert(ReferenceEquals(root.GetRequiredService<IAlpha>(), rootInstance),
                "the root still resolves its own registration after a child overrides it");
            return Task.CompletedTask;
        }

        private Task ParentSingletonSharedByReference()
        {
            var rootContainer = new DiContainer();
            rootContainer.AddSingleton<Counter, Counter>();
            var root = rootContainer.BuildServiceProvider();

            // The child registers nothing for Counter, so it must delegate to the parent.
            var child = new DiContainer().BuildServiceProvider(root);

            Assert(ReferenceEquals(child.GetRequiredService<Counter>(), root.GetRequiredService<Counter>()),
                "a parent singleton resolved via the child is the same reference as resolved via the root");
            return Task.CompletedTask;
        }

        private Task ChildGetServicesOverlaysAfterParent()
        {
            var rootFoo = new Foo();
            var childFoo = new OtherFoo();

            var rootContainer = new DiContainer();
            rootContainer.AddSingleton<IFoo>(rootFoo);
            var root = rootContainer.BuildServiceProvider();

            var childContainer = new DiContainer();
            childContainer.AddSingleton<IFoo>(childFoo);
            var child = childContainer.BuildServiceProvider(root);

            var all = new List<IFoo>(child.GetServices<IFoo>());
            Assert(all.Count == 2, "GetServices on a child returns the parent and child registrations (count == 2)");
            Assert(ReferenceEquals(all[0], rootFoo), "GetServices on a child lists the parent registration first");
            Assert(ReferenceEquals(all[1], childFoo),
                "GetServices on a child overlays its registration after the parent's");
            Assert(ReferenceEquals(child.GetRequiredService<IFoo>(), childFoo),
                "GetService on a child returns the child registration (overlaid on root)");
            return Task.CompletedTask;
        }

        private Task SiblingChildrenAreIsolated()
        {
            var root = new DiContainer().BuildServiceProvider();

            var childAContainer = new DiContainer();
            childAContainer.AddSingleton<IAlpha>(new Alpha());
            var childA = childAContainer.BuildServiceProvider(root);

            var childBContainer = new DiContainer();
            childBContainer.AddSingleton<IFoo>(new Foo());
            var childB = childBContainer.BuildServiceProvider(root);

            Assert(childA.GetService<IAlpha>() != null, "sibling child A sees its own IAlpha registration");
            Assert(childA.GetService<IFoo>() == null, "sibling child A does not see child B's child-only IFoo");
            Assert(childB.GetService<IFoo>() != null, "sibling child B sees its own IFoo registration");
            Assert(childB.GetService<IAlpha>() == null, "sibling child B does not see child A's child-only IAlpha");
            return Task.CompletedTask;
        }

        private Task ConcurrentChildCreationIsSafe()
        {
            var rootContainer = new DiContainer();
            rootContainer.AddSingleton<Counter, Counter>();
            var root = rootContainer.BuildServiceProvider();
            var sharedCounter = root.GetRequiredService<Counter>();

            const int childCount = 64;
            var ownResolvedCorrectly = new bool[childCount];
            var parentSharedCorrectly = new bool[childCount];
            var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();

            // Create N children on N threads and resolve from each: no shared mutable state, so this must
            // neither throw nor cross-contaminate (each child sees its own registration + the one shared root singleton).
            Parallel.For(0, childCount, i =>
            {
                try
                {
                    var marker = new Alpha();
                    var childContainer = new DiContainer();
                    childContainer.AddSingleton<IAlpha>(marker);
                    var child = childContainer.BuildServiceProvider(root);

                    ownResolvedCorrectly[i] = ReferenceEquals(child.GetRequiredService<IAlpha>(), marker);
                    parentSharedCorrectly[i] = ReferenceEquals(child.GetRequiredService<Counter>(), sharedCounter);
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex);
                }
            });

            Assert(failures.IsEmpty, "concurrent child creation and resolution throws no exceptions");
            Assert(Array.TrueForAll(ownResolvedCorrectly, ok => ok),
                "each concurrently created child resolves its own child registration independently");
            Assert(Array.TrueForAll(parentSharedCorrectly, ok => ok),
                "each concurrently created child shares the one root singleton by reference");
            return Task.CompletedTask;
        }

        private Task ActivatorCreatesUnregisteredTypeWithDependencyAndProvider()
        {
            var alpha = new Alpha();
            var container = new DiContainer();
            container.AddSingleton<IAlpha>(alpha);
            var provider = container.BuildServiceProvider();

            // Activatable is never registered; ActivatorUtilities must still construct it from the provider,
            // injecting the registered IAlpha and the provider itself for the IServiceProvider parameter.
            var activated = (Activatable)ActivatorUtilities.CreateInstance(provider, typeof(Activatable));

            Assert(activated != null, "ActivatorUtilities activates an unregistered concrete type");
            Assert(ReferenceEquals(activated.Alpha, alpha),
                "the registered dependency is injected into the unregistered type");
            Assert(ReferenceEquals(activated.Provider, provider),
                "an IServiceProvider constructor parameter receives the provider itself");
            return Task.CompletedTask;
        }

        private Task ActivatorThrowsWhenDependencyMissing()
        {
            var provider = new DiContainer().BuildServiceProvider();

            // NeedsMissingDependency's only constructor needs an unregistered IFoo, so no constructor is
            // satisfiable — the same rule the registered-type path applies.
            Throws<InvalidOperationException>(
                () => ActivatorUtilities.CreateInstance(provider, typeof(NeedsMissingDependency)),
                "activating a type whose constructor needs a missing dependency throws",
                "suitable constructor");
            return Task.CompletedTask;
        }

        private Task ActivatorThrowsOnAmbiguousConstructor()
        {
            var container = new DiContainer();
            container.AddSingleton<IAlpha, Alpha>();
            container.AddSingleton<IBeta, Beta>();
            var provider = container.BuildServiceProvider();

            // Both Ambiguous constructors ((IAlpha) and (IBeta)) are satisfiable and equal-arity, so the
            // shared selection logic reports an ambiguous tie — exactly as for a registered type.
            Throws<InvalidOperationException>(
                () => ActivatorUtilities.CreateInstance(provider, typeof(Ambiguous)),
                "activating a type with an equal-arity ambiguous constructor throws",
                "Multiple constructors");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Asserts (via the base <see cref="ConformanceSuite.Assert(bool, string)"/>) that
        /// <paramref name="action"/> throws a <typeparamref name="TException"/>. When
        /// <paramref name="messageContains"/> is supplied, the thrown exception's message must also contain
        /// it (case-insensitive). Kept here so later tasks can encode their "throws at Build()" /
        /// "throws on missing dependency" acceptances as named PASS/FAIL lines.
        /// </summary>
        private void Throws<TException>(Action action, string name, string messageContains = null)
            where TException : Exception
        {
            try
            {
                action();
                Assert(false, $"{name} (expected {typeof(TException).Name}, but nothing was thrown)");
            }
            catch (TException ex)
            {
                var matches = messageContains == null
                    || ex.Message.IndexOf(messageContains, StringComparison.OrdinalIgnoreCase) >= 0;
                Assert(matches, name);
            }
            catch (Exception ex)
            {
                Assert(false, $"{name} (expected {typeof(TException).Name}, but got {ex.GetType().Name})");
            }
        }

        // -- Fixtures ------------------------------------------------------------------------

        private interface IAlpha { }

        private sealed class Alpha : IAlpha { }

        private interface IFoo { }

        private sealed class Foo : IFoo { }

        private sealed class OtherFoo : IFoo { }

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

        // Never registered: only ActivatorUtilities can construct it, injecting the registered IAlpha and
        // the provider itself for the IServiceProvider parameter.
        private sealed class Activatable
        {
            public IAlpha Alpha { get; }
            public IServiceProvider Provider { get; }

            public Activatable(IAlpha alpha, IServiceProvider provider)
            {
                Alpha = alpha;
                Provider = provider;
            }
        }

        // Its only constructor needs an unregistered IFoo, so no constructor is satisfiable.
        private sealed class NeedsMissingDependency
        {
            public NeedsMissingDependency(IFoo foo) { _ = foo; }
        }
    }
}
