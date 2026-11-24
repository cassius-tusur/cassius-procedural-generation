using System;
using System.Collections.Generic;
using LocationGenerator.Contracts;
using LocationGenerator.Strategies;
using Xunit;

namespace LocationGenerator.Tests.Strategies
{
    public class LocationStrategyRegistryTests
    {
        [Fact]
        public void Resolve_RejectsUnknownAndMismatchedOptions()
        {
            var registry = new LocationStrategyRegistry(new[] { new TestStrategy("custom") });

            var unknown = Assert.Throws<LocationGenerationValidationException>(() =>
                registry.Resolve("missing", new LinearOptions()));
            Assert.Contains(unknown.Diagnostics, diagnostic => diagnostic.Code == "strategy.unknown");

            var mismatch = Assert.Throws<LocationGenerationValidationException>(() =>
                registry.Resolve("custom", new LinearOptions()));
            Assert.Contains(mismatch.Diagnostics, diagnostic => diagnostic.Code == "options.typeMismatch");
        }

        [Fact]
        public void Registry_RejectsDuplicateKeys()
        {
            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new LocationStrategyRegistry(new[] { new TestStrategy("custom"), new TestStrategy("custom") }));

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "strategy.duplicate");
        }

        [Fact]
        public void Resolve_CustomStrategyNeedsNoBuiltinChanges()
        {
            var strategy = new TestStrategy("custom");
            var registry = new LocationStrategyRegistry(new[] { strategy });

            Assert.Same(strategy, registry.Resolve("custom", new TestOptions()));
        }

        [Fact]
        public void CreateDefault_ResolvesLoopWithLoopOptions()
        {
            var strategy = LocationStrategyRegistry.CreateDefault().Resolve("Loop", new LoopOptions(1, 3));

            Assert.IsType<LoopTopologyStrategy>(strategy);
        }

        private sealed class TestOptions : ILocationTopologyOptions { }

        private sealed class TestStrategy : ILocationTopologyStrategy
        {
            public TestStrategy(string strategyId) { StrategyId = strategyId; }
            public string StrategyId { get; }
            public Type OptionsType => typeof(TestOptions);
            public void ValidateOptions(ILocationTopologyOptions options) { }
            public LocationStrategyPlan Build(LocationStrategyContext context, ILocationTopologyOptions options)
                => new LocationStrategyPlan(Array.Empty<LocationPassageProposal>(), context.AttachmentId);
            public void ValidatePlan(LocationStrategyContext context, ILocationTopologyOptions options,
                LocationStrategyPlan plan) { }
        }
    }
}
