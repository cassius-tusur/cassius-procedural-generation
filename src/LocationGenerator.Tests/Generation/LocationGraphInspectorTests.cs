using System;
using System.Linq;
using BaseClasses.Interface;
using BaseClasses.Model.Params;
using LocationGenerator.Contracts;
using LocationGenerator.Generation;
using LocationGenerator.Params;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Generation
{
    public class LocationGraphInspectorTests
    {
        [Fact]
        public void Append_InspectsOnlyImmediateArea()
        {
            var graph = new TopologyGraphFixture();
            var root = graph.Location();
            var a = graph.Location();
            var b = graph.Location();
            var c = graph.Location();
            graph.Contains(root, a);
            graph.Contains(a, b);
            graph.Contains(b, c);
            graph.Contains(c, b);

            var scope = new LocationGraphInspector().Inspect(graph.Plot, root, LocationGenerationMode.Append);

            Assert.Equal(new[] { a.Id }, scope.ExistingGraph.NodeIds);
            Assert.Empty(scope.RemovedLocations);
        }

        [Fact]
        public void Append_RejectsImmediateContainsCycleAndExternalSecondParent()
        {
            var cycle = new TopologyGraphFixture();
            var root = cycle.Location();
            var a = cycle.Location();
            cycle.Contains(root, a);
            cycle.Contains(a, root);
            AssertGraphRejected(cycle, root, LocationGenerationMode.Append);

            var shared = new TopologyGraphFixture();
            root = shared.Location();
            a = shared.Location();
            var outside = shared.Location();
            shared.Contains(root, a);
            shared.Contains(outside, a);
            AssertGraphRejected(shared, root, LocationGenerationMode.Append);
        }

        [Fact]
        public void Overwrite_RejectsDeepCycleButAppendDoesNotTraverseIt()
        {
            var graph = new TopologyGraphFixture();
            var root = graph.Location();
            var a = graph.Location();
            var b = graph.Location();
            var c = graph.Location();
            graph.Contains(root, a);
            graph.Contains(a, b);
            graph.Contains(b, c);
            graph.Contains(c, b);

            AssertGraphRejected(graph, root, LocationGenerationMode.Overwrite);
        }

        [Fact]
        public void Append_RejectsMissingNonLocationAndInvalidLocalPassages()
        {
            var missing = new TopologyGraphFixture();
            var root = missing.Location();
            missing.Contains(root, missing.MissingLocation());
            AssertGraphRejected(missing, root, LocationGenerationMode.Append);

            var nonLocation = new TopologyGraphFixture();
            root = nonLocation.Location();
            nonLocation.Contains(root, nonLocation.Item());
            AssertGraphRejected(nonLocation, root, LocationGenerationMode.Append);

            var passages = new TopologyGraphFixture();
            root = passages.Location();
            var a = passages.Location();
            var b = passages.Location();
            passages.Contains(root, a);
            passages.Contains(root, b);
            passages.Passage(a, b);
            AssertGraphRejected(passages, root, LocationGenerationMode.Append);
            passages.Passage(b, a);
            passages.Passage(a, a);
            AssertGraphRejected(passages, root, LocationGenerationMode.Append);
        }

        [Fact]
        public void Append_IgnoresFalseRelationsAndExternalPassages()
        {
            var graph = new TopologyGraphFixture();
            var root = graph.Location();
            var a = graph.Location();
            var b = graph.Location();
            var outside = graph.Location();
            graph.Contains(root, a);
            graph.Contains(root, b, active: false);
            graph.Passage(a, outside);
            graph.Passage(outside, a);
            graph.Passage(a, b, active: false);

            var scope = new LocationGraphInspector().Inspect(graph.Plot, root, LocationGenerationMode.Append);

            Assert.Equal(new[] { a.Id }, scope.ExistingGraph.NodeIds);
            Assert.Empty(scope.ExistingGraph.Passages);
        }

        [Fact]
        public void Inspect_UsesIdIdentityAndRejectsAmbiguousIdsInAffectedArea()
        {
            var graph = new TopologyGraphFixture();
            var root = graph.Location();
            var child = graph.Location();
            graph.Contains(root, child);
            var scope = new LocationGraphInspector().Inspect(graph.Plot, root, LocationGenerationMode.Append);
            Assert.Same(child, scope.ExistingLocations[child.Id]);

            var duplicate = new TopologyGraphFixture();
            root = duplicate.Location();
            child = duplicate.Location();
            duplicate.Contains(root, child);
            duplicate.InjectDuplicateIdLocation(child.Id);
            AssertGraphRejected(duplicate, root, LocationGenerationMode.Append);
        }

        [Fact]
        public void Overwrite_RejectsEveryCrossBoundaryRelationAndDescribesIt()
        {
            var graph = new TopologyGraphFixture();
            var root = graph.Location();
            var child = graph.Location();
            var outside = graph.Location();
            var quest = new ParamKey<string>("Quest", "World");
            graph.Contains(root, child);
            graph.Passage(child, outside);
            graph.Passage(outside, child);
            graph.Relate(child, outside, quest, "active");
            graph.Relate(outside, child, quest, "incoming");
            graph.Relate(child, outside, LocationRelationKeys.Entrance, false);
            graph.Entrance(root, child);

            var exception = Assert.Throws<LocationGenerationValidationException>(() =>
                new LocationGraphInspector().Inspect(graph.Plot, root, LocationGenerationMode.Overwrite));

            var diagnostic = Assert.Single(exception.Diagnostics, d => d.Code == "overwrite.externalReference");
            Assert.Contains(diagnostic.Relations, relation => relation.SourceId == child.Id &&
                relation.TargetId == outside.Id && relation.Namespace == "Base" && relation.Name == "Passage");
            Assert.Contains(diagnostic.Relations, relation => relation.SourceId == child.Id &&
                relation.TargetId == outside.Id && relation.Namespace == "World" && relation.Name == "Quest");
            Assert.DoesNotContain(diagnostic.Relations, relation => relation.SourceId == root.Id &&
                relation.TargetId == child.Id && relation.Name == "Contains");
        }

        [Fact]
        public void Overwrite_AllowsRootRelationsAndCollectsAllInternalRelationsForRemoval()
        {
            var graph = new TopologyGraphFixture();
            var root = graph.Location();
            var child = graph.Location();
            var grandchild = graph.Location();
            var outside = graph.Location();
            var quest = new ParamKey<string>("Quest", "World");
            graph.Contains(root, child);
            graph.Contains(child, grandchild);
            graph.Passage(child, grandchild);
            graph.Relate(child, grandchild, quest, "keep-with-subtree");
            graph.Passage(root, outside);
            graph.Passage(outside, root);

            var scope = new LocationGraphInspector().Inspect(graph.Plot, root, LocationGenerationMode.Overwrite);

            Assert.Equal(new[] { child.Id, grandchild.Id }.OrderBy(id => id),
                scope.RemovedLocations.Select(location => location.Id).OrderBy(id => id));
            Assert.Contains(scope.RemovedRelations, relation => relation.Param.Equals(quest));
            Assert.Contains(scope.RemovedRelations, relation => relation.Source.Id == root.Id &&
                relation.Target.Id == child.Id && relation.Param.Equals(BaseClasses.Services.Binds.BaseRelationKeys.Contains));
        }

        [Fact]
        public void Inspect_DoesNotMutatePlot()
        {
            var graph = new TopologyGraphFixture();
            var root = graph.Location();
            var child = graph.Location();
            graph.Contains(root, child);
            var elements = graph.Plot.Elements.ToArray();
            var relations = graph.Plot.Relations.ToArray();

            new LocationGraphInspector().Inspect(graph.Plot, root, LocationGenerationMode.Overwrite);

            Assert.Equal(elements, graph.Plot.Elements);
            Assert.Equal(relations, graph.Plot.Relations);
            Assert.All(relations, relation => Assert.Same(relation, graph.Plot.Relations.Single(item => ReferenceEquals(item, relation))));
        }

        private static void AssertGraphRejected(TopologyGraphFixture graph, IElement root, LocationGenerationMode mode)
        {
            Assert.Throws<LocationGenerationValidationException>(() =>
                new LocationGraphInspector().Inspect(graph.Plot, root, mode));
        }
    }
}
