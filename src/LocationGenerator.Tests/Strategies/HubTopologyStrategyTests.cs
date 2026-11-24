using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;
using LocationGenerator.Strategies;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Strategies
{
    public class HubTopologyStrategyTests
    {
        private static readonly Guid[] NewIds =
        {
            Guid.Parse("00000000-0000-0000-0000-000000000201"),
            Guid.Parse("00000000-0000-0000-0000-000000000202"),
            Guid.Parse("00000000-0000-0000-0000-000000000203"),
            Guid.Parse("00000000-0000-0000-0000-000000000204"),
            Guid.Parse("00000000-0000-0000-0000-000000000205"),
            Guid.Parse("00000000-0000-0000-0000-000000000206"),
            Guid.Parse("00000000-0000-0000-0000-000000000207")
        };

        private static readonly Guid[] AppendIds =
        {
            Guid.Parse("00000000-0000-0000-0000-000000000301"),
            Guid.Parse("00000000-0000-0000-0000-000000000302")
        };

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(7)]
        public void Hub_NewGraphIsStar(int nodeCount)
        {
            var nodes = NewIds.Take(nodeCount).ToArray();
            var plan = new HubTopologyStrategy().Build(Context(Array.Empty<Guid>(), nodes), new HubOptions());

            Assert.Null(plan.AttachmentId);
            Assert.Equal(nodeCount - 1, plan.Passages.Count);
            TopologyAssertions.AssertConnected(nodes, plan.Passages);
            Assert.Equal(0, TopologyAssertions.CycleRank(nodes, plan.Passages));
            var degrees = TopologyAssertions.Degrees(nodes, plan.Passages);
            Assert.Equal(nodeCount - 1, degrees.Values.Max());
            Assert.Equal(nodeCount == 2 ? 2 : 1,
                degrees.Count(pair => pair.Value == nodeCount - 1));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(7)]
        public void Hub_AppendAddsOnlySpokes(int oldCount)
        {
            var old = NewIds.Take(oldCount).ToArray();
            var oldEdges = StarEdges(old);
            var created = AppendIds;
            var attachment = oldCount == 2 ? old[1] : (Guid?)null;
            var plan = new HubTopologyStrategy().Build(
                Context(old, created, attachment, oldEdges), new HubOptions());
            var allNodes = old.Concat(created).ToArray();
            var allEdges = oldEdges.Concat(plan.Passages).ToArray();

            Assert.Equal(oldCount == 2 ? old[1] : old[0], plan.AttachmentId);
            Assert.Equal(allNodes.Length - 1, allEdges.Length);
            TopologyAssertions.AssertConnected(allNodes, allEdges);
            var degrees = TopologyAssertions.Degrees(allNodes, allEdges);
            Assert.Equal(allNodes.Length - 1, degrees[plan.AttachmentId!.Value]);
            Assert.DoesNotContain(plan.Passages, edge =>
                created.Contains(edge.FirstId) && created.Contains(edge.SecondId));
        }

        [Fact]
        public void Hub_AppendAcceptsEitherCenterForTwoVertices()
        {
            var old = NewIds.Take(2).ToArray();
            var edges = StarEdges(old);
            foreach (var center in old)
            {
                var plan = new HubTopologyStrategy().Build(
                    Context(old, new[] { NewIds[2] }, center, edges), new HubOptions());
                Assert.Equal(center, plan.AttachmentId);
                Assert.Contains(plan.Passages, edge =>
                    (edge.FirstId == center && edge.SecondId == NewIds[2]) ||
                    (edge.SecondId == center && edge.FirstId == NewIds[2]));
            }
        }

        [Fact]
        public void Hub_AppendRejectsLeafWhenThreeOrMoreOldVertices()
        {
            var old = NewIds.Take(3).ToArray();
            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new HubTopologyStrategy().Build(
                    Context(old, new[] { NewIds[3] }, old[1], StarEdges(old)), new HubOptions()));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "attachment.unavailable");
        }

        [Fact]
        public void Hub_AppendRejectsAdditionalOldLink()
        {
            var old = NewIds.Take(3).ToArray();
            var edges = StarEdges(old).Concat(new[] { new LocationPassageProposal(old[1], old[2]) }).ToArray();
            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new HubTopologyStrategy().Build(
                    Context(old, new[] { NewIds[3] }, null, edges), new HubOptions()));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "graph.incompatible");
        }

        [Fact]
        public void Hub_ValidatePlanRejectsAttachmentNotUsedBySpokes()
        {
            var old = NewIds.Take(2).ToArray();
            var context = Context(old, new[] { NewIds[2] }, old[0], StarEdges(old));
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old[1], NewIds[2]) }, old[0]);

            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new HubTopologyStrategy().ValidatePlan(context, new HubOptions(), proposal));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "attachment.unavailable");
        }

        private static IReadOnlyList<LocationPassageProposal> StarEdges(IReadOnlyList<Guid> nodes)
        {
            return nodes.Skip(1).Select(node => new LocationPassageProposal(nodes[0], node)).ToArray();
        }

        private static LocationStrategyContext Context(IReadOnlyList<Guid> existing,
            IReadOnlyList<Guid> created, Guid? attachment = null,
            IReadOnlyList<LocationPassageProposal>? passages = null)
        {
            return new LocationStrategyContext(
                new LocationGraphSnapshot(existing, passages ?? Array.Empty<LocationPassageProposal>()),
                created, attachment, new SequenceRandom(0));
        }
    }
}
