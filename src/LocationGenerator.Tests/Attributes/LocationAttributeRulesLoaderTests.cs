using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using LocationGenerator.Attributes;
using Xunit;

namespace LocationGenerator.Tests.Attributes
{
    public class LocationAttributeRulesLoaderTests
    {
        private const string ValidJson = """
            {
              "id": "test-правила",
              "biomes": ["crypt", "cavern"],
              "sizes": ["large", "small"],
              "densities": ["dense", "sparse"],
              "preservations": ["intact", "ruined"],
              "densityByBiomeAndSize": [
                {"biome": "crypt", "size": "large", "values": ["dense", "sparse"]},
                {"biome": "crypt", "size": "small", "values": ["sparse"]},
                {"biome": "cavern", "size": "large", "values": ["sparse"]},
                {"biome": "cavern", "size": "small", "values": ["dense"]}
              ],
              "preservationByBiomeAndDensity": [
                {"biome": "crypt", "density": "dense", "values": ["intact"]},
                {"biome": "crypt", "density": "sparse", "values": ["ruined", "intact"]},
                {"biome": "cavern", "density": "dense", "values": ["ruined"]},
                {"biome": "cavern", "density": "sparse", "values": ["intact", "ruined"]}
              ]
            }
            """;

        [Fact]
        public void LoadJson_MapsValuesInBothLookupTablesPreservingCatalogAndChoiceOrder()
        {
            AssertMappedRules(LocationAttributeRulesLoader.LoadJson(ValidJson));
        }

        [Fact]
        public void LoadFile_ReadsExplicitUtf8FileWithValuesInBothLookupTables()
        {
            WithFile(ValidJson, path => AssertMappedRules(LocationAttributeRulesLoader.LoadFile(path)));
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("{")]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("{\"id\": 42}")]
        [InlineData("{} {}")]
        [InlineData("id: rules")]
        public void LoadJson_RejectsMalformedOrNonObjectJsonWithParseCause(string json)
        {
            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadJson(json));

            Assert.Null(error.SourcePath);
            Assert.IsAssignableFrom<JsonException>(error.InnerException);
        }

        [Fact]
        public void LoadJson_NullInputPreservesArgumentCause()
        {
            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadJson(null!));

            Assert.Null(error.SourcePath);
            Assert.IsType<ArgumentNullException>(error.InnerException);
        }

        [Theory]
        [InlineData("densityByBiomeAndSize", "large")]
        [InlineData("preservationByBiomeAndDensity", "dense")]
        public void LoadJson_RejectsDuplicateLookupKeysEvenWhenChoicesDiffer(
            string tableName, string secondKey)
        {
            var json = JsonNode.Parse(ValidJson)!;
            var table = json[tableName]!.AsArray();
            var duplicate = table[0]!.DeepClone();
            duplicate["values"] = table[1]!["values"]!.DeepClone();
            table.Add(duplicate);

            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadJson(json.ToJsonString()));

            Assert.Null(error.SourcePath);
            Assert.NotNull(error.InnerException);
            Assert.Contains(tableName, error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("crypt", error.Message);
            Assert.Contains(secondKey, error.Message);
        }

        [Theory]
        [MemberData(nameof(InvalidRules))]
        public void LoadJson_RejectsInvalidContentUsingDomainValidator(string json, string diagnostic)
        {
            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadJson(json));

            Assert.Null(error.SourcePath);
            var cause = Assert.IsType<ArgumentException>(error.InnerException);
            Assert.Equal("rules", cause.ParamName);
            Assert.Contains(diagnostic, cause.Message);
            Assert.Contains(diagnostic, error.Message);
        }

        public static IEnumerable<object[]> InvalidRules()
        {
            yield return Invalid(json => json["id"] = " ", "Id is required");
            yield return Invalid(json => json["biomes"] = null, "Biomes");
            yield return Invalid(json => json.AsObject().Remove("sizes"), "Sizes");
            yield return Invalid(json => json["densities"]!.AsArray().Add("dense"), "duplicates");
            yield return Invalid(json => json["preservations"]!.AsArray().Add(" "), "blank");
            yield return Invalid(json => json["densityByBiomeAndSize"] = null, "DensityByBiomeAndSize");
            yield return Invalid(json => json.AsObject().Remove("preservationByBiomeAndDensity"),
                "PreservationByBiomeAndDensity");
            yield return Invalid(json => json["densityByBiomeAndSize"]!.AsArray().RemoveAt(0),
                "DensityByBiomeAndSize[crypt,large] is required");
            yield return Invalid(json => json["preservationByBiomeAndDensity"]!.AsArray().RemoveAt(0),
                "PreservationByBiomeAndDensity[crypt,dense] is required");
            yield return Invalid(json => json["densityByBiomeAndSize"]![0]!["values"] = new JsonArray(),
                "DensityByBiomeAndSize[crypt,large] is required");
            yield return Invalid(json => json["preservationByBiomeAndDensity"]![0]!["values"] = null,
                "PreservationByBiomeAndDensity[crypt,dense] is required");
            yield return Invalid(json => json["densityByBiomeAndSize"]![0]!["values"]![0] = "unknown",
                "unknown value");
            yield return Invalid(json => json["preservationByBiomeAndDensity"]![0]!["values"]![0] = "unknown",
                "unknown value");
            yield return Invalid(json => json["densityByBiomeAndSize"]![0]!["biome"] = "unknown",
                "uses an unknown value");
            yield return Invalid(json => json["densityByBiomeAndSize"]![0]!.AsObject().Remove("size"),
                "uses an unknown value");
            yield return Invalid(json => json["preservationByBiomeAndDensity"]![0]!["density"] = "unknown",
                "is unreachable");
            yield return Invalid(json => json["densityByBiomeAndSize"]![0]!["values"]!.AsArray().Add("dense"),
                "duplicates");
            yield return Invalid(json => json["preservationByBiomeAndDensity"]![0]!["values"]!.AsArray().Add("intact"),
                "duplicates");
        }

        [Theory]
        [InlineData("densityByBiomeAndSize")]
        [InlineData("preservationByBiomeAndDensity")]
        public void LoadJson_RejectsNullLookupEntries(string tableName)
        {
            var json = JsonNode.Parse(ValidJson)!;
            json[tableName]!.AsArray().Add((JsonNode?)null);

            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadJson(json.ToJsonString()));

            Assert.IsAssignableFrom<JsonException>(error.InnerException);
            Assert.Contains(tableName, error.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("{")]
        [InlineData("{}")]
        public void LoadFile_ReportsSourcePathAndOriginalParseOrValidationCause(string json)
        {
            WithFile(json, path =>
            {
                var error = Assert.Throws<LocationAttributeRulesLoadException>(
                    () => LocationAttributeRulesLoader.LoadFile(path));

                Assert.Equal(path, error.SourcePath);
                Assert.Contains(path, error.Message);
                Assert.NotNull(error.InnerException);
                Assert.IsNotType<LocationAttributeRulesLoadException>(error.InnerException);
                Assert.True(error.InnerException is JsonException || error.InnerException is ArgumentException);
            });
        }

        [Fact]
        public void LoadFile_MissingFilePreservesPathAndIoCause()
        {
            var path = Path.Combine(Path.GetTempPath(), $"missing-rules-{Guid.NewGuid():N}.json");

            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadFile(path));

            Assert.Equal(path, error.SourcePath);
            Assert.Contains(path, error.Message);
            Assert.IsType<FileNotFoundException>(error.InnerException);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("invalid\0path.json")]
        public void LoadFile_InvalidPathPreservesPathAndArgumentCause(string? path)
        {
            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadFile(path!));

            Assert.Equal(path, error.SourcePath);
            Assert.IsAssignableFrom<ArgumentException>(error.InnerException);
        }

        [Fact]
        public void LoadFile_DirectoryPathPreservesAccessCause()
        {
            var path = Path.GetTempPath();

            var error = Assert.Throws<LocationAttributeRulesLoadException>(
                () => LocationAttributeRulesLoader.LoadFile(path));

            Assert.Equal(path, error.SourcePath);
            Assert.IsType<UnauthorizedAccessException>(error.InnerException);
        }

        private static object[] Invalid(Action<JsonNode> mutate, string diagnostic)
        {
            var json = JsonNode.Parse(ValidJson)!;
            mutate(json);
            return new object[] { json.ToJsonString(), diagnostic };
        }

        private static void WithFile(string json, Action<string> action)
        {
            var path = Path.Combine(Path.GetTempPath(), $"location-rules-{Guid.NewGuid():N}.json");
            try
            {
                File.WriteAllText(path, json);
                action(path);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static void AssertMappedRules(LocationAttributeRules rules)
        {
            Assert.Equal("test-правила", rules.Id);
            Assert.Equal(new[] { "crypt", "cavern" }, rules.Biomes);
            Assert.Equal(new[] { "large", "small" }, rules.Sizes);
            Assert.Equal(new[] { "dense", "sparse" }, rules.Densities);
            Assert.Equal(new[] { "intact", "ruined" }, rules.Preservations);
            Assert.Equal(4, rules.DensityByBiomeAndSize.Count);
            Assert.Equal(new[] { "dense", "sparse" }, rules.DensityByBiomeAndSize[("crypt", "large")]);
            Assert.Equal(new[] { "sparse" }, rules.DensityByBiomeAndSize[("crypt", "small")]);
            Assert.Equal(new[] { "sparse" }, rules.DensityByBiomeAndSize[("cavern", "large")]);
            Assert.Equal(new[] { "dense" }, rules.DensityByBiomeAndSize[("cavern", "small")]);
            Assert.Equal(4, rules.PreservationByBiomeAndDensity.Count);
            Assert.Equal(new[] { "intact" }, rules.PreservationByBiomeAndDensity[("crypt", "dense")]);
            Assert.Equal(new[] { "ruined", "intact" }, rules.PreservationByBiomeAndDensity[("crypt", "sparse")]);
            Assert.Equal(new[] { "ruined" }, rules.PreservationByBiomeAndDensity[("cavern", "dense")]);
            Assert.Equal(new[] { "intact", "ruined" }, rules.PreservationByBiomeAndDensity[("cavern", "sparse")]);
        }
    }
}
