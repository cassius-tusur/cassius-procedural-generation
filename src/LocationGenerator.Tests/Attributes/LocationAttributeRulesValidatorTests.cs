using System;
using System.Collections.Generic;
using LocationGenerator.Attributes;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Attributes
{
    public class LocationAttributeRulesValidatorTests
    {
        [Fact]
        public void Validate_DungeonFlatTablesAreCompleteAndPreserveCryptAndCavernValues()
        {
            var rules = LocationAttributeRulesTestFixture.LoadDungeonFlatV1();
            LocationAttributeRulesValidator.Validate(rules);
            Assert.Equal(new[] { "crypt", "cavern" }, rules.Biomes);
            Assert.Equal(new[] { "small", "medium", "large" }, rules.Sizes);
            Assert.Equal(new[] { "sparse", "normal", "dense" }, rules.Densities);
            Assert.Equal(new[] { "ruined", "weathered", "intact" }, rules.Preservations);
            AssertTableEqual(new Dictionary<(string Biome, string Size), IReadOnlyList<string>>
            {
                [("crypt", "small")] = new[] { "sparse", "normal" },
                [("crypt", "medium")] = new[] { "normal", "dense" },
                [("crypt", "large")] = new[] { "normal", "dense" },
                [("cavern", "small")] = new[] { "sparse", "normal" },
                [("cavern", "medium")] = new[] { "sparse", "normal", "dense" },
                [("cavern", "large")] = new[] { "normal", "dense" }
            }, rules.DensityByBiomeAndSize);
            AssertTableEqual(new Dictionary<(string Biome, string Density), IReadOnlyList<string>>
            {
                [("crypt", "sparse")] = new[] { "ruined", "weathered" },
                [("crypt", "normal")] = new[] { "weathered", "intact" },
                [("crypt", "dense")] = new[] { "weathered", "intact" },
                [("cavern", "sparse")] = new[] { "ruined", "weathered" },
                [("cavern", "normal")] = new[] { "weathered", "intact" },
                [("cavern", "dense")] = new[] { "intact" }
            }, rules.PreservationByBiomeAndDensity);
        }

        [Fact]
        public void Validate_RejectsMalformedTables()
        {
            var rules = LocationAttributeRulesTestFixture.LoadDungeonFlatV1() with
            {
                DensityByBiomeAndSize = new Dictionary<(string Biome, string Size), IReadOnlyList<string>>()
            };
            Assert.Throws<ArgumentException>(() => LocationAttributeRulesValidator.Validate(rules));
        }

        private static void AssertTableEqual<TKey>(
            IReadOnlyDictionary<TKey, IReadOnlyList<string>> expected,
            IReadOnlyDictionary<TKey, IReadOnlyList<string>> actual) where TKey : notnull
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var (key, values) in expected)
            {
                Assert.True(actual.TryGetValue(key, out var actualValues));
                Assert.Equal(values, actualValues);
            }
        }
    }
}
