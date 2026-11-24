using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services;
using BaseClasses.Services.Binds;
using LocationGenerator.Contracts;
using LocationGenerator.Generation;
using LocationGenerator.Params;
using LocationGenerator.Presets;
using LocationGenerator.Queries;
using LocationGenerator.Strategies;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Serialization
{
    public class LocationTopologySerializationTests
    {
        [Fact]
        public void PlotRoundTrip_PreservesGeneratedTopologyAndPreExistingLegacyLocationData()
        {
            var plot = new Plot();
            var root = new Element(ElemType.Location);
            root.Params.Add(LocationParams.Biome, "crypt");
            var legacyChild = new Element(ElemType.Location);
            plot.Add(root);
            plot.Add(legacyChild);
            plot.Relations.Add(new Relation(root, legacyChild, BaseRelationKeys.Contains, true));
            plot.Relations.Add(new Relation(root, legacyChild, LocationRelationKeys.Entrance, true));
            var request = new LocationTopologyRequest("Linear", 2, new LinearOptions(), LocationGenerationMode.Append);
            new LocationTopologyGenerator(request, LocationStrategyRegistry.CreateDefault(), new SequenceRandom()).Generate(plot, root, request);

            var restored = Serializer.DeserializeString<Plot>(Serializer.SerializeToString(plot));
            var restoredRoot = restored.Elements.Single(x => x.Id == root.Id);
            Assert.Equal(3, LocationTopology.GetChildren(restored, restoredRoot).Count);
            Assert.Contains(restored.Relations, x => x.Param.Equals(BaseRelationKeys.Contains));
            Assert.Contains(restored.Relations, x => x.Param.Equals(BaseRelationKeys.Passage));
            Assert.True(restoredRoot.Params.TryGet(LocationParams.Biome, out string biome));
            Assert.Equal("crypt", biome);
            Assert.Contains(restored.Relations, relation =>
                relation.Param.Equals(LocationRelationKeys.Entrance) &&
                relation.Source.Id == restoredRoot.Id &&
                relation.Target.Id == legacyChild.Id &&
                relation.Value is bool active && active);
        }

        [Fact]
        public void PlotRoundTrip_PreservesLoopPresetStructureAlongsideLegacyScaleBiomeAndEntrance()
        {
            var plot = new Plot();
            var root = new Element(ElemType.Location);
            var legacyChild = new Element(ElemType.Location);
            root.Params.Add(LocationParams.Biome, "cavern");
            plot.Add(root);
            plot.Add(legacyChild);
            plot.Relations.Add(new Relation(root, legacyChild, BaseRelationKeys.Contains, true));
            plot.Relations.Add(new Relation(root, legacyChild, LocationRelationKeys.Entrance, true));
            var request = LocationTopologyPresets.DungeonFlatV1();
            new LocationTopologyGenerator(request, LocationStrategyRegistry.CreateDefault(), new SequenceRandom())
                .Generate(plot, root, request);

            var restored = Serializer.DeserializeString<Plot>(Serializer.SerializeToString(plot));
            var restoredRoot = restored.Elements.Single(element => element.Id == root.Id);
            var children = LocationTopology.GetChildren(restored, restoredRoot);

            Assert.Equal(9, children.Count);
            Assert.Equal(9, UndirectedPassageCount(restored, children));
            Assert.True(restoredRoot.Params.TryGet(LocationParams.Biome, out string biome));
            Assert.Equal("cavern", biome);
            Assert.NotNull(LocationTopology.GetEntrance(restored, restoredRoot));
        }

        private static int UndirectedPassageCount(Plot plot, System.Collections.Generic.IReadOnlyList<BaseClasses.Interface.IElement> locations)
        {
            var ids = locations.Select(location => location.Id).ToHashSet();
            return plot.Relations
                .Where(relation => relation.Param.Equals(BaseRelationKeys.Passage) &&
                    relation.Value is bool active && active && ids.Contains(relation.Source.Id) && ids.Contains(relation.Target.Id))
                .Select(relation => relation.Source.Id.CompareTo(relation.Target.Id) < 0
                    ? (relation.Source.Id, relation.Target.Id)
                    : (relation.Target.Id, relation.Source.Id))
                .Distinct()
                .Count();
        }
    }
}
