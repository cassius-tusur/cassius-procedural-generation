using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using LocationGenerator.Params;
using LocationGenerator.Queries;
using Xunit;

namespace LocationGenerator.Tests.Queries
{
    public class LocationTopologyTests
    {
        [Fact]
        public void GetReachableLocations_FollowsOnlyPassageRelationsFromEntrance()
        {
            var plot = new Plot();
            var root = NewLocation();
            var entry = NewLocation();
            var hall = NewLocation();
            var unconnected = NewLocation();
            Add(plot, root, entry, hall, unconnected);
            Relate(plot, root, entry, LocationRelationKeys.Entrance);
            Relate(plot, entry, hall, BaseClasses.Services.Binds.BaseRelationKeys.Passage);
            Relate(plot, entry, unconnected, new ParamKey<bool>("Door", "Other"));

            var reachable = LocationTopology.GetReachableLocations(plot, entry);

            Assert.Equal(new[] { entry, hall }, reachable);
            Assert.DoesNotContain(unconnected, reachable);
        }

        [Fact]
        public void Queries_UseOnlyStableRelationKeysAndDoNotMutatePlot()
        {
            var plot = new Plot();
            var root = NewLocation();
            var entry = NewLocation();
            var child = NewLocation();
            var neighbor = NewLocation();
            Add(plot, root, entry, child, neighbor);
            Relate(plot, root, entry, LocationRelationKeys.Entrance);
            Relate(plot, root, child, BaseClasses.Services.Binds.BaseRelationKeys.Contains);
            Relate(plot, neighbor, child, BaseClasses.Services.Binds.BaseRelationKeys.Passage);
            Relate(plot, root, neighbor, new ParamKey<bool>("Contains", "Other"));
            var elementsBefore = plot.Elements.ToArray();
            var relationsBefore = plot.Relations.ToArray();

            var entrance = LocationTopology.GetEntrance(plot, root);
            var children = LocationTopology.GetChildren(plot, root);
            var neighbors = LocationTopology.GetPassageNeighbors(plot, child);

            Assert.Same(entry, entrance);
            Assert.Equal(new[] { child }, children);
            Assert.Equal(new[] { neighbor }, neighbors);
            Assert.Equal(elementsBefore, plot.Elements);
            Assert.Equal(relationsBefore, plot.Relations);
        }

        [Fact]
        public void IsInSubtree_TraversesOnlyOutgoingContainsFromSuppliedRoot()
        {
            var plot = new Plot();
            var root = NewLocation();
            var child = NewLocation();
            var grandchild = NewLocation();
            var foreignParent = NewLocation();
            Add(plot, root, child, grandchild, foreignParent);
            Relate(plot, root, child, BaseClasses.Services.Binds.BaseRelationKeys.Contains);
            Relate(plot, child, grandchild, BaseClasses.Services.Binds.BaseRelationKeys.Contains);
            Relate(plot, foreignParent, root, BaseClasses.Services.Binds.BaseRelationKeys.Contains);

            Assert.True(LocationTopology.IsInSubtree(plot, root, root));
            Assert.True(LocationTopology.IsInSubtree(plot, root, grandchild));
            Assert.False(LocationTopology.IsInSubtree(plot, root, foreignParent));
        }

        [Fact]
        public void FalseContainsRelation_DoesNotMakeChildOrSubtreeMember()
        {
            var plot = new Plot();
            var root = NewLocation();
            var child = NewLocation();
            Add(plot, root, child);
            Relate(plot, root, child, BaseClasses.Services.Binds.BaseRelationKeys.Contains, false);

            Assert.Empty(LocationTopology.GetChildren(plot, root));
            Assert.False(LocationTopology.IsInSubtree(plot, root, child));
        }

        [Fact]
        public void FalsePassageRelation_DoesNotMakeNeighborOrReachableLocation()
        {
            var plot = new Plot();
            var entrance = NewLocation();
            var neighbor = NewLocation();
            Add(plot, entrance, neighbor);
            Relate(plot, entrance, neighbor, BaseClasses.Services.Binds.BaseRelationKeys.Passage, false);

            Assert.Empty(LocationTopology.GetPassageNeighbors(plot, entrance));
            Assert.Equal(new[] { entrance }, LocationTopology.GetReachableLocations(plot, entrance));
        }

        [Fact]
        public void FalseEntranceRelation_DoesNotIdentifyEntrance()
        {
            var plot = new Plot();
            var root = NewLocation();
            var entrance = NewLocation();
            Add(plot, root, entrance);
            Relate(plot, root, entrance, LocationRelationKeys.Entrance, false);

            Assert.Null(LocationTopology.GetEntrance(plot, root));
        }

        [Fact]
        public void GetEntrance_ReadsActiveLegacyEntranceWithoutTreatingContainsAsAnEntrance()
        {
            var plot = new Plot();
            var root = NewLocation();
            var contained = NewLocation();
            var entrance = NewLocation();
            Add(plot, root, contained, entrance);
            Relate(plot, root, contained, BaseClasses.Services.Binds.BaseRelationKeys.Contains);
            Relate(plot, root, entrance, LocationRelationKeys.Entrance);

            Assert.Same(entrance, LocationTopology.GetEntrance(plot, root));
        }

        [Fact]
        public void Queries_UseElementIdentityWhenDistinctLocationsHaveMatchingValues()
        {
            var plot = new Plot();
            var root = new Element(ElemType.Location, time: 7);
            var child = new Element(ElemType.Location, time: 8);
            var lookalikeRoot = new Element(ElemType.Location, time: 7);
            var foreignChild = new Element(ElemType.Location, time: 9);
            Add(plot, root, child, lookalikeRoot, foreignChild);
            Relate(plot, root, child, BaseClasses.Services.Binds.BaseRelationKeys.Contains);
            Relate(plot, lookalikeRoot, foreignChild, BaseClasses.Services.Binds.BaseRelationKeys.Contains);

            Assert.Equal(new[] { child }, LocationTopology.GetChildren(plot, root));
            Assert.False(LocationTopology.IsInSubtree(plot, root, foreignChild));
        }

        private static Element NewLocation()
        {
            return new Element(ElemType.Location);
        }

        private static void Add(Plot plot, params Element[] elements)
        {
            foreach (var element in elements) plot.Add(element);
        }

        private static void Relate(Plot plot, Element source, Element target, ParamKey<bool> key)
        {
            Relate(plot, source, target, key, true);
        }

        private static void Relate(Plot plot, Element source, Element target, ParamKey<bool> key, bool value)
        {
            plot.Relations.Add(new Relation(source, target, key, value));
        }
    }
}
