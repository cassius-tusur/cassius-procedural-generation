using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;
using LocationGenerator.Strategies;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Strategies
{
    public class BranchingTopologyStrategyTests
    {
        private static readonly Guid[] Ids = Enumerable.Range(1, 16)
            .Select(value => Guid.Parse($"00000000-0000-0000-0000-{value:D12}"))
            .ToArray();

        [Theory]
        [InlineData(1, 2)]
        [InlineData(2, 2)]
        [InlineData(3, 2)]
        [InlineData(4, 2)]
        [InlineData(7, 2)]
        [InlineData(1, 3)]
        [InlineData(2, 3)]
        [InlineData(3, 3)]
        [InlineData(4, 3)]
        [InlineData(7, 3)]
        public void NewGraph_IsBoundedTree(int nodeCount, int maxDegree)
        {
            var nodes = Ids.Take(nodeCount).ToArray();
            var plan = new BranchingTopologyStrategy().Build(Context(Array.Empty<Guid>(), nodes, null,
                Array.Empty<LocationPassageProposal>(), 0, 0, 0), new BranchingOptions(maxDegree));

            Assert.Null(plan.AttachmentId);
            Assert.Equal(nodeCount - 1, plan.Passages.Count);
            TopologyAssertions.AssertConnected(nodes, plan.Passages);
            Assert.Equal(0, TopologyAssertions.CycleRank(nodes, plan.Passages));
            var degrees = TopologyAssertions.Degrees(nodes, plan.Passages);
            Assert.All(degrees.Values, degree => Assert.InRange(degree, 0, maxDegree));
        }

        [Fact]
        public void NewGraph_RandomlySelectsParentsFromCreatedNodesWithCapacity()
        {
            var nodes = Ids.Take(6).ToArray();

            var plan = new BranchingTopologyStrategy().Build(
                Context(Array.Empty<Guid>(), nodes, null, Array.Empty<LocationPassageProposal>(),
                    0, 0, 0, 0),
                new BranchingOptions(3));

            Assert.Equal(new[]
            {
                Canonical(nodes[0], nodes[1]),
                Canonical(nodes[0], nodes[2]),
                Canonical(nodes[0], nodes[3]),
                Canonical(nodes[1], nodes[4]),
                Canonical(nodes[1], nodes[5])
            }, plan.Passages.Select(edge => Canonical(edge.FirstId, edge.SecondId)));
            Assert.Equal(2, TopologyAssertions.Degrees(nodes, plan.Passages).Values.Count(degree => degree >= 3));
        }

        [Fact]
        public void NewGraph_DoesNotGiveLeavesExtraSelectionWeight()
        {
            var nodes = Ids.Take(3).ToArray();

            var plan = new BranchingTopologyStrategy().Build(
                Context(Array.Empty<Guid>(), nodes, null, Array.Empty<LocationPassageProposal>(), 0),
                new BranchingOptions(3));

            Assert.Equal(new[]
            {
                Canonical(nodes[0], nodes[1]),
                Canonical(nodes[0], nodes[2])
            }, plan.Passages.Select(edge => Canonical(edge.FirstId, edge.SecondId)));
        }

        [Fact]
        public void NewGraph_SelectsParentsFromThreeLatestGenerations()
        {
            var nodes = Ids.Take(6).ToArray();

            var plan = new BranchingTopologyStrategy().Build(
                Context(Array.Empty<Guid>(), nodes, null, Array.Empty<LocationPassageProposal>(),
                    1, 2, 2, 0),
                new BranchingOptions(3));

            Assert.Equal(new[]
            {
                Canonical(nodes[0], nodes[1]),
                Canonical(nodes[1], nodes[2]),
                Canonical(nodes[2], nodes[3]),
                Canonical(nodes[3], nodes[4]),
                Canonical(nodes[2], nodes[5])
            }, plan.Passages.Select(edge => Canonical(edge.FirstId, edge.SecondId)));
        }

        [Fact]
        public void Append_PathOfThreePlusOneCanSelectFreeEnd()
        {
            var old = Ids.Take(3).ToArray();
            var path = Path(old);
            var plan = new BranchingTopologyStrategy().Build(
                Context(old, new[] { Ids[3] }, null, path, 0), new BranchingOptions(3));
            var allNodes = old.Concat(new[] { Ids[3] }).ToArray();
            var allPassages = path.Concat(plan.Passages).ToArray();

            Assert.Equal(old[0], plan.AttachmentId);
            Assert.Single(plan.Passages);
            TopologyAssertions.AssertConnected(allNodes, allPassages);
            Assert.All(TopologyAssertions.Degrees(allNodes, allPassages).Values,
                degree => Assert.InRange(degree, 0, 3));
        }

        [Fact]
        public void Append_PathOfThreePlusOneAcceptsExplicitFreeEnd()
        {
            var old = Ids.Take(3).ToArray();
            var plan = new BranchingTopologyStrategy().Build(
                Context(old, new[] { Ids[3] }, old[0], Path(old), 0), new BranchingOptions(3));

            Assert.Equal(old[0], plan.AttachmentId);
        }

        [Fact]
        public void Append_BranchMayBeInOldGraphOrNewFragment()
        {
            var oldBranch = new[] { Ids[0], Ids[1], Ids[2], Ids[3] };
            var oldBranchEdges = new[]
            {
                new LocationPassageProposal(oldBranch[0], oldBranch[1]),
                new LocationPassageProposal(oldBranch[0], oldBranch[2]),
                new LocationPassageProposal(oldBranch[0], oldBranch[3])
            };
            var oldBranchPlan = new BranchingTopologyStrategy().Build(
                Context(oldBranch, new[] { Ids[4] }, null, oldBranchEdges, 0), new BranchingOptions(3));
            Assert.DoesNotContain(oldBranchPlan.Passages, edge =>
                edge.FirstId == oldBranch[0] || edge.SecondId == oldBranch[0]);

            var oldPath = Ids.Take(2).ToArray();
            var fragmentPlan = new BranchingTopologyStrategy().Build(
                Context(oldPath, Ids.Skip(2).Take(3).ToArray(), null, Path(oldPath), 0, 0), new BranchingOptions(3));
            var allNodes = oldPath.Concat(Ids.Skip(2).Take(3)).ToArray();
            var allPassages = Path(oldPath).Concat(fragmentPlan.Passages).ToArray();
            Assert.Contains(TopologyAssertions.Degrees(allNodes, allPassages).Values, degree => degree >= 3);
        }

        [Fact]
        public void Append_ReservesDegreeAtBothBridgeEnds()
        {
            var old = Ids.Take(3).ToArray();
            var plan = new BranchingTopologyStrategy().Build(
                Context(old, Ids.Skip(3).Take(2).ToArray(), null, Path(old), 0), new BranchingOptions(2));
            var allNodes = old.Concat(Ids.Skip(3).Take(2)).ToArray();
            var allPassages = Path(old).Concat(plan.Passages).ToArray();
            var degrees = TopologyAssertions.Degrees(allNodes, allPassages);
            var bridge = Assert.Single(plan.Passages, edge => old.Contains(edge.FirstId) || old.Contains(edge.SecondId));
            var newEnd = old.Contains(bridge.FirstId) ? bridge.SecondId : bridge.FirstId;

            Assert.True(degrees[plan.AttachmentId!.Value] <= 2);
            Assert.True(degrees[newEnd] <= 2);
        }

        [Fact]
        public void Append_RejectsSaturatedAttachment()
        {
            var old = Ids.Take(3).ToArray();
            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new BranchingTopologyStrategy().Build(
                    Context(old, new[] { Ids[3] }, old[1], Path(old), 0), new BranchingOptions(2)));

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "attachment.unavailable");
        }

        [Fact]
        public void ValidatePlan_RejectsAttachmentThatDoesNotMatchBridge()
        {
            var old = Ids.Take(3).ToArray();
            var context = Context(old, new[] { Ids[3] }, old[1], Path(old), 0);
            var invalid = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old[0], Ids[3]) }, old[1]);

            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new BranchingTopologyStrategy().ValidatePlan(context, new BranchingOptions(3), invalid));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "attachment.unavailable");
        }

        private static IReadOnlyList<LocationPassageProposal> Path(IReadOnlyList<Guid> nodes) =>
            nodes.Skip(1).Select((node, index) => new LocationPassageProposal(nodes[index], node)).ToArray();

        private static (Guid First, Guid Second) Canonical(Guid first, Guid second) =>
            first.CompareTo(second) < 0 ? (first, second) : (second, first);

        private static LocationStrategyContext Context(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> created,
            Guid? attachment, IReadOnlyList<LocationPassageProposal> passages, params int[] random) =>
            new LocationStrategyContext(new LocationGraphSnapshot(existing, passages), created, attachment,
                new SequenceRandom(random));
    }
}
