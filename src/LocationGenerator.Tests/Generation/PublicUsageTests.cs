using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Model;
using BaseClasses.Services.Binds;
using LocationGenerator.Attributes;
using LocationGenerator.Contracts;
using LocationGenerator.Generation;
using LocationGenerator.Params;
using LocationGenerator.Presets;
using LocationGenerator.Queries;
using LocationGenerator.Strategies;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Generation
{
    public class PublicUsageTests
    {
        [Fact]
        public void PublicUsage_ParameterlessCityCanContainThenExpandAChosenChild()
        {
            var plot = new Plot();
            var city = new Element(ElemType.Location);
            plot.Add(city);
            var hub = new LocationTopologyRequest("Hub", 3, new HubOptions(), LocationGenerationMode.Append);
            var linear = new LocationTopologyRequest("Linear", 2, new LinearOptions(), LocationGenerationMode.Append);
            var generator = new LocationTopologyGenerator(hub, LocationStrategyRegistry.CreateDefault(), new SequenceRandom());

            var cityResult = generator.Generate(plot, city, hub);
            var keep = cityResult.CreatedLocations.Last();
            var keepResult = generator.Generate(plot, keep, linear);

            Assert.Equal(3, LocationTopology.GetChildren(plot, city).Count);
            Assert.Equal(2, LocationTopology.GetChildren(plot, keep).Count);
            Assert.Equal(2, keepResult.CreatedLocations.Count);
        }

        [Fact]
        public void PublicUsage_AttributeFailureDoesNotUndoCommittedTopology()
        {
            var plot = new Plot();
            var root = new Element(ElemType.Location);
            plot.Add(root);
            var request = new LocationTopologyRequest("Linear", 2, new LinearOptions(), LocationGenerationMode.Append);
            var topology = new LocationTopologyGenerator(request, LocationStrategyRegistry.CreateDefault(), new SequenceRandom());
            var result = topology.Generate(plot, root, request);
            var beforeIds = result.CreatedLocations.Select(x => x.Id).OrderBy(x => x).ToArray();
            result.CreatedLocations[0].Params.Add(LocationParams.Biome, "forest");
            var attributes = new LocationAttributeGenerator(LocationAttributeRulesTestFixture.LoadDungeonFlatV1(), new SequenceRandom());

            Assert.Throws<System.ArgumentException>(() => attributes.Generate(plot, result.CreatedLocations[0]));

            Assert.Equal(beforeIds, LocationTopology.GetChildren(plot, root).Select(x => x.Id).OrderBy(x => x));
            Assert.All(result.CreatedLocations, child => Assert.Contains(child, plot.Elements));
        }

        [Fact]
        public void CityFortressTower_ExpandsSameElementsWithoutMakingContainsPassable()
        {
            var plot = new Plot();
            var city = new Element(ElemType.Location);
            plot.Add(city);
            var generator = new LocationTopologyGenerator(LocationTopologyPresets.Settlement(),
                LocationStrategyRegistry.CreateDefault(), new SequenceRandom());

            var cityResult = generator.Generate(plot, city, LocationTopologyPresets.Settlement());
            var cityCenter = cityResult.CreatedLocations.First();
            var fortress = cityResult.CreatedLocations.Last();
            var fortressResult = generator.Generate(plot, fortress,
                LocationTopologyPresets.BranchingLair() with { NodeCount = 8 });
            var tower = fortressResult.CreatedLocations.Last();
            var towerResult = generator.Generate(plot, tower, LocationTopologyPresets.LinearAdventure());

            Assert.Same(fortress, plot.Elements.Single(element => element.Id == fortress.Id));
            Assert.Same(tower, plot.Elements.Single(element => element.Id == tower.Id));
            Assert.Equal(7, LocationTopology.GetChildren(plot, city).Count);
            Assert.Equal(8, LocationTopology.GetChildren(plot, fortress).Count);
            Assert.Equal(5, LocationTopology.GetChildren(plot, tower).Count);
            Assert.Contains(fortress, LocationTopology.GetPassageNeighbors(plot, cityCenter));
            Assert.Contains(cityCenter, LocationTopology.GetPassageNeighbors(plot, fortress));
            Assert.DoesNotContain(fortressResult.CreatedLocations[0], LocationTopology.GetPassageNeighbors(plot, fortress));
            Assert.Equal(5, towerResult.CreatedLocations.Count);
        }

        [Fact]
        public void PublicUsage_AppendAllowsCompatibleStrategyChangeAndRejectsIncompatibleGraph()
        {
            var plot = new Plot();
            var root = new Element(ElemType.Location);
            plot.Add(root);
            var generator = new LocationTopologyGenerator(LocationTopologyPresets.LinearAdventure(),
                LocationStrategyRegistry.CreateDefault(), new SequenceRandom());

            var linear = generator.Generate(plot, root, LocationTopologyPresets.LinearAdventure());
            var branching = generator.Generate(plot, root, LocationTopologyPresets.BranchingLair());

            Assert.Equal(5, linear.CreatedLocations.Count);
            Assert.Equal(7, branching.CreatedLocations.Count);
            Assert.Equal(12, LocationTopology.GetChildren(plot, root).Count);

            var starPlot = new Plot();
            var starRoot = new Element(ElemType.Location);
            starPlot.Add(starRoot);
            generator.Generate(starPlot, starRoot, LocationTopologyPresets.Settlement() with { NodeCount = 4 });

            var error = Assert.Throws<LocationGenerationValidationException>(() =>
                generator.Generate(starPlot, starRoot, LocationTopologyPresets.LinearAdventure()));

            Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "graph.incompatible");
            Assert.Equal(4, LocationTopology.GetChildren(starPlot, starRoot).Count);
        }

        [Fact]
        public void PublicUsage_LegacyEntranceSurvivesAppendButBlocksOverwriteUntilExplicitlyRemoved()
        {
            var plot = new Plot();
            var root = new Element(ElemType.Location);
            var legacyChild = new Element(ElemType.Location);
            plot.Add(root);
            plot.Add(legacyChild);
            plot.Bind(root, legacyChild, BaseRelationKeys.Contains, true);
            var entrance = new Relation(root, legacyChild, LocationRelationKeys.Entrance, true);
            plot.Relations.Add(entrance);
            var generator = new LocationTopologyGenerator(LocationTopologyPresets.LinearAdventure(),
                LocationStrategyRegistry.CreateDefault(), new SequenceRandom());

            generator.Generate(plot, root, LocationTopologyPresets.LinearAdventure());
            Assert.Same(legacyChild, LocationTopology.GetEntrance(plot, root));

            var overwrite = LocationTopologyPresets.LinearAdventure() with { Mode = LocationGenerationMode.Overwrite };
            var error = Assert.Throws<LocationGenerationValidationException>(() => generator.Generate(plot, root, overwrite));
            Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "overwrite.externalReference");
            Assert.Contains(legacyChild, plot.Elements);

            plot.Relations.Remove(entrance);
            var result = generator.Generate(plot, root, overwrite);
            Assert.Equal(5, result.CreatedLocations.Count);
            Assert.DoesNotContain(legacyChild, plot.Elements);
        }
    }
}
