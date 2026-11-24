using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;
using LocationGenerator.Strategies;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Strategies
{
    public class LinearTopologyStrategyTests
    {
        private static readonly Guid[] NewIds =
        {
            Guid.Parse("00000000-0000-0000-0000-000000000101"),
            Guid.Parse("00000000-0000-0000-0000-000000000102"),
            Guid.Parse("00000000-0000-0000-0000-000000000103"),
            Guid.Parse("00000000-0000-0000-0000-000000000104"),
            Guid.Parse("00000000-0000-0000-0000-000000000105")
        };

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        public void Linear_NewGraphIsPath(int nodeCount)
        {
            var nodes = NewIds.Take(nodeCount).ToArray();
            var plan = new LinearTopologyStrategy().Build(Context(Array.Empty<Guid>(), nodes), new LinearOptions());

            Assert.Null(plan.AttachmentId);
            Assert.Equal(nodeCount - 1, plan.Passages.Count);
            TopologyAssertions.AssertConnected(nodes, plan.Passages);
            Assert.Equal(0, TopologyAssertions.CycleRank(nodes, plan.Passages));
            Assert.Equal(nodeCount <= 2 ? nodeCount - 1 : 2,
                TopologyAssertions.Degrees(nodes, plan.Passages).Values.Max());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Linear_AppendUsesEndpoint(bool explicitAttachment)
        {
            var old = new[]
            {
                Guid.Parse("00000000-0000-0000-0000-000000000003"),
                Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Guid.Parse("00000000-0000-0000-0000-000000000002")
            };
            var oldEdges = new[]
            {
                new LocationPassageProposal(old[0], old[1]),
                new LocationPassageProposal(old[1], old[2])
            };
            var attachment = old[2];
            var context = Context(old, NewIds.Take(2).ToArray(), explicitAttachment ? attachment : (Guid?)null,
                new[] { explicitAttachment ? 0 : 1 }, oldEdges);
            var plan = new LinearTopologyStrategy().Build(context, new LinearOptions());
            var allNodes = old.Concat(NewIds.Take(2)).ToArray();
            var allEdges = oldEdges.Concat(plan.Passages).ToArray();

            Assert.Equal(explicitAttachment ? attachment : old[0], plan.AttachmentId);
            Assert.Equal(allNodes.Length - 1, allEdges.Length);
            TopologyAssertions.AssertConnected(allNodes, allEdges);
            Assert.Equal(0, TopologyAssertions.CycleRank(allNodes, allEdges));
        }

        [Fact]
        public void Linear_AppendUsesOnlyVertexWhenOneOldNodeExists()
        {
            var old = NewIds[0];
            var plan = new LinearTopologyStrategy().Build(
                Context(new[] { old }, NewIds.Skip(1).Take(2).ToArray(), null), new LinearOptions());

            Assert.Equal(old, plan.AttachmentId);
            Assert.Contains(plan.Passages, edge =>
                (edge.FirstId == old && edge.SecondId == NewIds[1]) ||
                (edge.SecondId == old && edge.FirstId == NewIds[1]));
        }

        [Fact]
        public void Linear_AppendRejectsInnerAttachmentAndExistingCycle()
        {
            var old = NewIds.Take(3).ToArray();
            var path = new[]
            {
                new LocationPassageProposal(old[0], old[1]),
                new LocationPassageProposal(old[1], old[2])
            };
            var inner = Assert.Throws<LocationGenerationValidationException>(() =>
                new LinearTopologyStrategy().Build(
                    Context(old, new[] { NewIds[3] }, old[1], new[] { 0 }, path), new LinearOptions()));
            Assert.Contains(inner.Diagnostics, diagnostic => diagnostic.Code == "attachment.unavailable");

            var cycle = path.Concat(new[] { new LocationPassageProposal(old[2], old[0]) }).ToArray();
            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new LinearTopologyStrategy().Build(
                    Context(old, new[] { NewIds[3] }, null, new[] { 0 }, cycle), new LinearOptions()));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "graph.incompatible");
        }

        [Fact]
        public void Linear_ValidatePlanRejectsShapeWithoutUsingRandom()
        {
            var old = NewIds.Take(2).ToArray();
            var context = Context(old, new[] { NewIds[2] }, old[1], 0,
                new[] { new LocationPassageProposal(old[0], old[1]) });
            var invalid = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old[0], old[1]) }, old[1]);

            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new LinearTopologyStrategy().ValidatePlan(context, new LinearOptions(), invalid));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "graph.incompatible");
        }

        [Fact]
        public void Linear_ValidatePlanRejectsAttachmentNotUsedByBridge()
        {
            var old = NewIds.Take(2).ToArray();
            var context = Context(old, new[] { NewIds[2] }, old[0], new[] { 0 },
                new[] { new LocationPassageProposal(old[0], old[1]) });
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old[1], NewIds[2]) }, old[0]);

            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new LinearTopologyStrategy().ValidatePlan(context, new LinearOptions(), proposal));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "attachment.unavailable");
        }

        private static LocationStrategyContext Context(IReadOnlyList<Guid> existing,
            IReadOnlyList<Guid> created, Guid? attachment = null, params int[] random)
        {
            return Context(existing, created, attachment, random, Array.Empty<LocationPassageProposal>());
        }

        private static LocationStrategyContext Context(IReadOnlyList<Guid> existing,
            IReadOnlyList<Guid> created, Guid? attachment, int randomValue,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            return Context(existing, created, attachment, new[] { randomValue }, passages);
        }

        private static LocationStrategyContext Context(IReadOnlyList<Guid> existing,
            IReadOnlyList<Guid> created, Guid? attachment, int[] random,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            return new LocationStrategyContext(new LocationGraphSnapshot(existing, passages), created,
                attachment, new SequenceRandom(random));
        }
    }
}
