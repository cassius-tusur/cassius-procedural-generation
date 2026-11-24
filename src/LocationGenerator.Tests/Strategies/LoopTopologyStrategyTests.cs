using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LocationGenerator.Contracts;
using LocationGenerator.Strategies;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Strategies
{
    public class LoopTopologyStrategyTests
    {
        private static readonly Guid[] Ids = Enumerable.Range(1, 32)
            .Select(value => Guid.Parse($"00000000-0000-0000-0000-{value:D12}"))
            .ToArray();

        [Theory]
        [InlineData(1, 0, 2)]
        [InlineData(2, 0, 2)]
        [InlineData(3, 1, 2)]
        [InlineData(8, 1, 3)]
        [InlineData(8, 2, 3)]
        public void NewGraph_HasExactConnectedDegreeBoundedShape(int nodeCount, int cycleCount, int maxDegree)
        {
            var nodes = Ids.Take(nodeCount).ToArray();
            var plan = new LoopTopologyStrategy().Build(Context(Array.Empty<Guid>(), nodes),
                new LoopOptions(cycleCount, maxDegree));

            Assert.Null(plan.AttachmentId);
            Assert.Equal(nodeCount - 1 + cycleCount, plan.Passages.Count);
            AssertSimpleConnectedBounded(nodes, plan.Passages, cycleCount, maxDegree);
        }

        [Fact]
        public void NewGraph_UsesBranchingRandomTreeAsItsSpanningTree()
        {
            var nodes = Ids.Take(6).ToArray();
            var random = new[] { 1, 0, 1, 0 };
            var branching = new BranchingTopologyStrategy().Build(
                Context(Array.Empty<Guid>(), nodes, null, null, random), new BranchingOptions(3));
            var loop = new LoopTopologyStrategy().Build(
                Context(Array.Empty<Guid>(), nodes, null, null, random), new LoopOptions(1, 3));

            var loopEdges = loop.Passages.Select(edge => Canonical(edge.FirstId, edge.SecondId)).ToHashSet();
            Assert.All(branching.Passages,
                edge => Assert.Contains(Canonical(edge.FirstId, edge.SecondId), loopEdges));
        }

        [Fact]
        public void Append_ReservesBridgeCapacity()
        {
            var old = Ids.Take(1).ToArray();
            var created = Ids.Skip(1).Take(3).ToArray();

            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
            {
                new LoopTopologyStrategy().Build(Context(old, created), new LoopOptions(1, 2));
            });

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "graph.incompatible" ||
                diagnostic.Code == "attachment.unavailable");
        }

        [Fact]
        public void Append_WithBridgeCapacityAddsBridgeAndOnlyRequestedCycles()
        {
            var old = Ids.Take(2).ToArray();
            var oldPassages = new[] { new LocationPassageProposal(old[0], old[1]) };
            var created = Ids.Skip(2).Take(3).ToArray();
            var plan = new LoopTopologyStrategy().Build(Context(old, created, null, oldPassages, 0),
                new LoopOptions(1, 3));
            var allNodes = old.Concat(created).ToArray();
            var allPassages = oldPassages.Concat(plan.Passages).ToArray();

            Assert.NotNull(plan.AttachmentId);
            Assert.Equal(created.Length + 1, plan.Passages.Count);
            Assert.Equal(1, TopologyAssertions.CycleRank(allNodes, allPassages) -
                TopologyAssertions.CycleRank(old, oldPassages));
            Assert.Single(plan.Passages, edge =>
                old.Contains(edge.FirstId) != old.Contains(edge.SecondId));
            AssertSimpleConnectedBounded(allNodes, allPassages, 1, 3);
        }

        [Theory]
        [InlineData(-1, 2)]
        [InlineData(0, 1)]
        [InlineData(2, 2)]
        public void Options_RejectInvalidCycleOrDegree(int cycleCount, int maxDegree)
        {
            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
            {
                new LoopTopologyStrategy().ValidateOptions(new LoopOptions(cycleCount, maxDegree));
            });

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "options.invalid");
        }

        [Fact]
        public void Build_RejectsTooManyEdgesUsingLongArithmetic()
        {
            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
            {
                new DegreeBoundedGraphBuilder().Build(Ids.Take(2).ToArray(), int.MaxValue, int.MaxValue,
                    null, new SequenceRandom(0));
            });

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "graph.incompatible");
        }

        [Fact]
        public void Append_RejectsSaturatedOldGraph()
        {
            var old = Ids.Take(3).ToArray();
            var triangle = new[]
            {
                new LocationPassageProposal(old[0], old[1]),
                new LocationPassageProposal(old[1], old[2]),
                new LocationPassageProposal(old[2], old[0])
            };

            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
            {
                new LoopTopologyStrategy().Build(Context(old, new[] { Ids[3] }, null, triangle),
                    new LoopOptions(0, 2));
            });

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "attachment.unavailable");
        }

        [Fact]
        public void Builder_AgreesWithIndependentExhaustiveOracleForEverySmallSimpleGraphCase()
        {
            for (var nodeCount = 1; nodeCount <= 5; nodeCount++)
            for (var maxDegree = 2; maxDegree <= 4; maxDegree++)
            for (var cycles = 0; cycles <= 6; cycles++)
            for (var reserve = 0; reserve <= 1; reserve++)
            {
                var nodes = Ids.Take(nodeCount).ToArray();
                var expected = SmallGraphOracle.IsFeasible(nodeCount, cycles, maxDegree, reserve == 1);
                var builder = new DegreeBoundedGraphBuilder();
                if (!expected)
                {
                    Assert.Throws<LocationGenerationValidationException>(() =>
                    {
                        builder.Build(nodes, cycles, maxDegree,
                            reserve == 1 ? nodes[0] : (Guid?)null, new SequenceRandom(0));
                    });
                    continue;
                }

                var passages = builder.Build(nodes, cycles, maxDegree,
                    reserve == 1 ? nodes[0] : (Guid?)null, new SequenceRandom(0));
                Assert.Equal(nodeCount - 1 + cycles, passages.Count);
                AssertSimpleConnectedBounded(nodes, passages, cycles, maxDegree,
                    reserve == 1 ? nodes[0] : (Guid?)null);
            }
        }

        [Fact]
        public void Builder_ReservedAttachmentConsumesCapacityAtSpecifiedInputNode()
        {
            var nodes = Ids.Take(4).ToArray();
            var reservedEndpoint = nodes[3];
            var passages = new DegreeBoundedGraphBuilder().Build(nodes, 2, 3, reservedEndpoint,
                new SequenceRandom(1, 1, 1));

            AssertSimpleConnectedBounded(nodes, passages, 2, 3, reservedEndpoint);
            Assert.Equal(2, TopologyAssertions.Degrees(nodes, passages)[reservedEndpoint]);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(1, 1, 1, 1)]
        [InlineData(1, 1, 2, 1)]
        public void Builder_FixedRandomOrdersRemainCompleteForDenseFeasibleGraph(params int[] random)
        {
            var nodes = Ids.Take(5).ToArray();
            var passages = new DegreeBoundedGraphBuilder().Build(nodes, 5, 4, nodes[0],
                new SequenceRandom(random));

            AssertSimpleConnectedBounded(nodes, passages, 5, 4, nodes[0]);
        }

        [Fact]
        public void Builder_ReproducibilityUsesInputIndexesRatherThanGuidOrdering()
        {
            var first = Ids.Take(5).ToArray();
            var second = new[] { Ids[12], Ids[4], Ids[15], Ids[7], Ids[10] };
            var random = new[] { 1, 1, 1, 0 };
            var builder = new DegreeBoundedGraphBuilder();

            var firstEdges = ToIndexEdges(first, builder.Build(first, 3, 3, null, new SequenceRandom(random)));
            var secondEdges = ToIndexEdges(second, builder.Build(second, 3, 3, null, new SequenceRandom(random)));

            Assert.Equal(firstEdges, secondEdges);
        }

        [Fact]
        public void Builder_PresetAndDenseSmallCasesCompletePromptly()
        {
            var cases = new[] { (8, 1, 3), (8, 2, 3), (5, 5, 4), (5, 6, 4) };
            var stopwatch = Stopwatch.StartNew();
            foreach (var testCase in cases)
            {
                var nodes = Ids.Take(testCase.Item1).ToArray();
                var passages = new DegreeBoundedGraphBuilder().Build(nodes, testCase.Item2, testCase.Item3,
                    null, new SequenceRandom(0));
                AssertSimpleConnectedBounded(nodes, passages, testCase.Item2, testCase.Item3);
            }
            stopwatch.Stop();

            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
                $"Loop preset/dense-small construction took {stopwatch.Elapsed}.");
        }

        private static LocationStrategyContext Context(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> created,
            Guid? attachment = null, IReadOnlyList<LocationPassageProposal>? passages = null, params int[] random) =>
            new LocationStrategyContext(new LocationGraphSnapshot(existing,
                passages ?? Array.Empty<LocationPassageProposal>()), created, attachment, new SequenceRandom(random));

        private static void AssertSimpleConnectedBounded(IReadOnlyList<Guid> nodes,
            IReadOnlyList<LocationPassageProposal> passages, int cycles, int maxDegree, Guid? reserved = null)
        {
            Assert.Equal(passages.Count, passages.Select(edge => Canonical(edge.FirstId, edge.SecondId)).Distinct().Count());
            TopologyAssertions.AssertConnected(nodes, passages);
            Assert.Equal(cycles, TopologyAssertions.CycleRank(nodes, passages));
            var degrees = TopologyAssertions.Degrees(nodes, passages);
            Assert.All(degrees, pair => Assert.InRange(pair.Value, 0, pair.Key == reserved ? maxDegree - 1 : maxDegree));
        }

        private static (int First, int Second)[] ToIndexEdges(IReadOnlyList<Guid> nodes,
            IReadOnlyList<LocationPassageProposal> passages) => passages.Select(edge =>
            {
                var first = IndexOf(nodes, edge.FirstId);
                var second = IndexOf(nodes, edge.SecondId);
                return first < second ? (first, second) : (second, first);
            }).OrderBy(edge => edge.Item1).ThenBy(edge => edge.Item2).ToArray();

        private static int IndexOf(IReadOnlyList<Guid> nodes, Guid id)
        {
            for (var index = 0; index < nodes.Count; index++)
                if (nodes[index] == id) return index;
            throw new InvalidOperationException("Passage referred to a node outside the builder input.");
        }

        private static (Guid First, Guid Second) Canonical(Guid first, Guid second) =>
            first.CompareTo(second) < 0 ? (first, second) : (second, first);
    }
}
