using System;
using BaseClasses.Enum;
using BaseClasses.Model;
using LocationGenerator.Attributes;
using LocationGenerator.Generation;
using LocationGenerator.Params;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Generation
{
    public class LocationAttributeGeneratorTests
    {
        [Fact]
        public void Generate_CompleteRulesWorkWithoutScaleOrPlotMembership()
        {
            var scene = WithBiome("crypt");
            var generator = new LocationAttributeGenerator(LocationAttributeRulesTestFixture.LoadDungeonFlatV1(), new SequenceRandom(0, 0, 0));
            Assert.Same(scene, generator.Generate(new Plot(), scene));
            Assert.Equal("small", Read(scene, LocationParams.Size));
            Assert.Equal("sparse", Read(scene, LocationParams.Density));
            Assert.Equal("ruined", Read(scene, LocationParams.Preservation));
        }

        [Theory]
        [InlineData(null)] [InlineData("")] [InlineData("forest")]
        public void Generate_MissingOrUnknownBiomeFailsWithoutAddingAttributes(string? biome)
        {
            var scene = new Element(ElemType.Location);
            if (biome != null) scene.Params.Add(LocationParams.Biome, biome);
            var generator = new LocationAttributeGenerator(LocationAttributeRulesTestFixture.LoadDungeonFlatV1(), new SequenceRandom());
            Assert.Throws<ArgumentException>(() => generator.Generate(new Plot(), scene));
            Assert.False(scene.Params.ContainsKey(LocationParams.Size));
            Assert.False(scene.Params.ContainsKey(LocationParams.Density));
            Assert.False(scene.Params.ContainsKey(LocationParams.Preservation));
        }

        [Fact]
        public void Generate_ExplicitDifferentBiomesUseTheirOwnTables()
        {
            var crypt = WithBiome("crypt"); var cavern = WithBiome("cavern");
            var generator = new LocationAttributeGenerator(LocationAttributeRulesTestFixture.LoadDungeonFlatV1(), new SequenceRandom(0, 0, 0, 1, 1, 1));
            generator.Generate(new Plot(), crypt); generator.Generate(new Plot(), cavern);
            Assert.Equal("small", Read(crypt, LocationParams.Size)); Assert.Equal("sparse", Read(crypt, LocationParams.Density)); Assert.Equal("ruined", Read(crypt, LocationParams.Preservation));
            Assert.Equal("medium", Read(cavern, LocationParams.Size)); Assert.Equal("normal", Read(cavern, LocationParams.Density)); Assert.Equal("intact", Read(cavern, LocationParams.Preservation));
        }

        private static Element WithBiome(string biome) { var element = new Element(ElemType.Location); element.Params.Add(LocationParams.Biome, biome); return element; }
        private static string Read(Element element, BaseClasses.Model.Params.ParamKey<string> key) { Assert.True(element.Params.TryGet(key, out var value)); return value; }
    }
}
