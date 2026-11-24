using System;
using System.Collections.Generic;
using System.Linq;

namespace LocationGenerator.Attributes
{
    /// <summary>
    /// Validates location-attribute catalogs for completeness and consistency.
    /// </summary>
    public static class LocationAttributeRulesValidator
    {
        /// <summary>
        /// Validates the location attribute rules and their allowed values.
        /// </summary>
        /// <param name="rules">The rules to validate or apply.</param>
        /// <exception cref="ArgumentException">Thrown when an argument is invalid.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public static void Validate(LocationAttributeRules rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(rules.Id)) errors.Add("Id is required.");
            var biomes = Catalog(rules.Biomes, "Biomes", errors);
            var sizes = Catalog(rules.Sizes, "Sizes", errors);
            var densities = Catalog(rules.Densities, "Densities", errors);
            var preservations = Catalog(rules.Preservations, "Preservations", errors);

            var reachable = biomes.ToDictionary(biome => biome,
                _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            if (rules.DensityByBiomeAndSize == null)
                errors.Add("DensityByBiomeAndSize is required.");
            else
            {
                foreach (var biome in biomes)
                foreach (var size in sizes)
                {
                    if (!rules.DensityByBiomeAndSize.TryGetValue((biome, size), out var choices) ||
                        choices == null || choices.Count == 0)
                    {
                        errors.Add($"DensityByBiomeAndSize[{biome},{size}] is required.");
                        continue;
                    }
                    foreach (var choice in Choices(choices, densities,
                                 $"DensityByBiomeAndSize[{biome},{size}]", errors))
                        reachable[biome].Add(choice);
                }
                foreach (var key in rules.DensityByBiomeAndSize.Keys)
                    if (!biomes.Contains(key.Biome) || !sizes.Contains(key.Size))
                        errors.Add($"DensityByBiomeAndSize[{key.Biome},{key.Size}] uses an unknown value.");
            }

            if (rules.PreservationByBiomeAndDensity == null)
                errors.Add("PreservationByBiomeAndDensity is required.");
            else
            {
                foreach (var biome in biomes)
                foreach (var density in reachable[biome])
                {
                    if (!rules.PreservationByBiomeAndDensity.TryGetValue((biome, density), out var choices) ||
                        choices == null || choices.Count == 0)
                    {
                        errors.Add($"PreservationByBiomeAndDensity[{biome},{density}] is required.");
                        continue;
                    }
                    Choices(choices, preservations,
                        $"PreservationByBiomeAndDensity[{biome},{density}]", errors).ToArray();
                }
                foreach (var key in rules.PreservationByBiomeAndDensity.Keys)
                    if (!reachable.TryGetValue(key.Biome, out var values) || !values.Contains(key.Density))
                        errors.Add($"PreservationByBiomeAndDensity[{key.Biome},{key.Density}] is unreachable.");
            }

            if (errors.Count > 0)
                throw new ArgumentException(string.Join(" ", errors), nameof(rules));
        }

        /// <summary>
        /// Creates a lookup set from the supplied values.
        /// </summary>
        /// <param name="values">The values to process.</param>
        /// <param name="name">The name value.</param>
        /// <param name="errors">The errors value.</param>
        /// <returns>A set containing the distinct supplied values.</returns>
        private static HashSet<string> Catalog(IReadOnlyList<string>? values, string name,
            ICollection<string> errors)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (values == null || values.Count == 0)
            {
                errors.Add(name + " must contain at least one value.");
                return result;
            }
            foreach (var value in values)
                if (string.IsNullOrWhiteSpace(value)) errors.Add(name + " must not contain blank values.");
                else if (!result.Add(value)) errors.Add(name + " must not contain duplicates.");
            return result;
        }

        /// <summary>
        /// Creates a validated list of attribute choices.
        /// </summary>
        /// <param name="values">The values to process.</param>
        /// <param name="catalog">The catalog value.</param>
        /// <param name="name">The name value.</param>
        /// <param name="errors">The errors value.</param>
        /// <returns>The resulting collection.</returns>
        private static IEnumerable<string> Choices(IReadOnlyList<string> values, ISet<string> catalog,
            string name, ICollection<string> errors)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                if (string.IsNullOrWhiteSpace(value)) errors.Add(name + " must not contain blank values.");
                else if (!catalog.Contains(value)) errors.Add(name + " contains an unknown value.");
                else if (!result.Add(value)) errors.Add(name + " must not contain duplicates.");
            }
            return result;
        }
    }
}
