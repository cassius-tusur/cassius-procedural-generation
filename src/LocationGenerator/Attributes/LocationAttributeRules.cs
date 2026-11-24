using System;
using System.Collections.Generic;

namespace LocationGenerator.Attributes
{
    /// <summary>
    /// Defines allowed location attributes and their biome-dependent combinations.
    /// </summary>
    public sealed record LocationAttributeRules
    {
        public string Id { get; init; } = string.Empty;
        public IReadOnlyList<string> Biomes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Sizes { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Densities { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> Preservations { get; init; } = Array.Empty<string>();
        public IReadOnlyDictionary<(string Biome, string Size), IReadOnlyList<string>>
            DensityByBiomeAndSize { get; init; } =
                new Dictionary<(string Biome, string Size), IReadOnlyList<string>>();
        public IReadOnlyDictionary<(string Biome, string Density), IReadOnlyList<string>>
            PreservationByBiomeAndDensity { get; init; } =
                new Dictionary<(string Biome, string Density), IReadOnlyList<string>>();
    }
}
