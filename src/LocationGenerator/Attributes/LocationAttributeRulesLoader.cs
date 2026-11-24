using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security;
using System.Text.Json;

namespace LocationGenerator.Attributes
{
    public static class LocationAttributeRulesLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>Loads and validates rules from the explicitly supplied file path.</summary>
        /// <exception cref="LocationAttributeRulesLoadException">The file cannot be read or its rules are invalid.</exception>
        public static LocationAttributeRules LoadFile(string path)
        {
            string json;
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    throw new ArgumentException("A rules file path is required.", nameof(path));
                json = File.ReadAllText(path);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException ||
                                          error is ArgumentException || error is NotSupportedException ||
                                          error is SecurityException)
            {
                throw LoadError(path, error);
            }

            return Load(json, path);
        }

        /// <summary>Loads and validates rules from camel-case JSON without performing file I/O.</summary>
        /// <exception cref="LocationAttributeRulesLoadException">The JSON or its rules are invalid.</exception>
        public static LocationAttributeRules LoadJson(string json) => Load(json, null);

        private static LocationAttributeRules Load(string json, string? sourcePath)
        {
            try
            {
                var dto = JsonSerializer.Deserialize<LocationAttributeRulesJsonDto>(json, JsonOptions)
                          ?? throw new JsonException("Rules must be a JSON object.");
                var rules = Convert(dto);
                LocationAttributeRulesValidator.Validate(rules);
                return rules;
            }
            catch (Exception error) when (error is JsonException || error is ArgumentException)
            {
                throw LoadError(sourcePath, error);
            }
        }

        private static LocationAttributeRules Convert(LocationAttributeRulesJsonDto dto)
        {
            var densities = new Dictionary<(string Biome, string Size), IReadOnlyList<string>>();
            foreach (var entry in dto.DensityByBiomeAndSize ?? Array.Empty<LocationAttributeRulesJsonDto.DensityEntry>())
            {
                if (entry == null)
                    throw new JsonException("DensityByBiomeAndSize must not contain null entries.");
                var key = (entry.Biome ?? string.Empty, entry.Size ?? string.Empty);
                if (!densities.TryAdd(key, Values(entry.Values)))
                    throw new JsonException($"Duplicate DensityByBiomeAndSize[{key.Item1},{key.Item2}] entry.");
            }

            var preservations = new Dictionary<(string Biome, string Density), IReadOnlyList<string>>();
            foreach (var entry in dto.PreservationByBiomeAndDensity ?? Array.Empty<LocationAttributeRulesJsonDto.PreservationEntry>())
            {
                if (entry == null)
                    throw new JsonException("PreservationByBiomeAndDensity must not contain null entries.");
                var key = (entry.Biome ?? string.Empty, entry.Density ?? string.Empty);
                if (!preservations.TryAdd(key, Values(entry.Values)))
                    throw new JsonException($"Duplicate PreservationByBiomeAndDensity[{key.Item1},{key.Item2}] entry.");
            }

            return new LocationAttributeRules
            {
                Id = dto.Id ?? string.Empty,
                Biomes = Values(dto.Biomes),
                Sizes = Values(dto.Sizes),
                Densities = Values(dto.Densities),
                Preservations = Values(dto.Preservations),
                DensityByBiomeAndSize =
                    new ReadOnlyDictionary<(string Biome, string Size), IReadOnlyList<string>>(densities),
                PreservationByBiomeAndDensity =
                    new ReadOnlyDictionary<(string Biome, string Density), IReadOnlyList<string>>(preservations)
            };
        }

        private static IReadOnlyList<string> Values(string[]? values) =>
            Array.AsReadOnly(values ?? Array.Empty<string>());

        private static LocationAttributeRulesLoadException LoadError(string? sourcePath, Exception error)
        {
            var source = sourcePath == null ? "JSON" : $"file '{sourcePath}'";
            return new LocationAttributeRulesLoadException(
                $"Could not load location attribute rules from {source}: {error.Message}", sourcePath, error);
        }
    }
}
