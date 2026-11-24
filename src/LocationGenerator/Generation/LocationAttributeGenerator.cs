using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using LocationGenerator.Attributes;
using LocationGenerator.Contracts;
using LocationGenerator.Params;

namespace LocationGenerator.Generation
{
    /// <summary>
    /// Populates prepared locations from a validated attribute-rule catalog.
    /// </summary>
    /// <seealso cref="LocationAttributeRules"/>
    public sealed class LocationAttributeGenerator : IGenerator
    {
        private readonly LocationAttributeRules _rules;
        private readonly ILocationRandom _random;

        /// <summary>
        /// Initializes a new instance of <see cref="LocationAttributeGenerator"/>.
        /// </summary>
        /// <param name="rules">The rules to validate or apply.</param>
        /// <param name="random">The random source used to make deterministic choices.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public LocationAttributeGenerator(LocationAttributeRules rules, ILocationRandom random)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            LocationAttributeRulesValidator.Validate(rules);
        }

        /// <summary>
        /// Generates location attributes and writes them to the prepared element.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="preparedElement">The prepared location element to populate.</param>
        /// <returns>The populated location element.</returns>
        /// <exception cref="ArgumentException">Thrown when an argument is invalid.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public IElement Generate(Plot plot, IElement preparedElement)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (preparedElement == null) throw new ArgumentNullException(nameof(preparedElement));
            if (preparedElement.Type != ElemType.Location)
                throw new ArgumentException("The prepared element must be a location.", nameof(preparedElement));
            if (!preparedElement.Params.TryGet(LocationParams.Biome, out var biome) ||
                string.IsNullOrWhiteSpace(biome))
                throw new ArgumentException("The prepared location must contain a biome.", nameof(preparedElement));
            if (!_rules.Biomes.Contains(biome))
                throw new ArgumentException($"Biome '{biome}' is not defined by rules '{_rules.Id}'.",
                    nameof(preparedElement));

            var size = Choose(_rules.Sizes);
            var density = Choose(_rules.DensityByBiomeAndSize[(biome, size)]);
            var preservation = Choose(_rules.PreservationByBiomeAndDensity[(biome, density)]);

            Set(preparedElement.Params, LocationParams.Size, size);
            Set(preparedElement.Params, LocationParams.Density, density);
            Set(preparedElement.Params, LocationParams.Preservation, preservation);
            return preparedElement;
        }

        /// <summary>
        /// Generates async.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="preparedElement">The prepared location element to populate.</param>
        /// <returns>A task that represents the asynchronous operation and contains its result.</returns>
        /// <exception cref="ArgumentException">Thrown when the prepared element is not a valid location.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        /// <seealso cref="Generate"/>
        public Task<IElement> GenerateAsync(Plot plot, IElement preparedElement) =>
            Task.FromResult(Generate(plot, preparedElement));

        /// <summary>
        /// Chooses one value from the supplied list using the configured random source.
        /// </summary>
        /// <param name="values">The values to process.</param>
        /// <returns>The resulting string value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be completed in the current state.</exception>
        private string Choose(IReadOnlyList<string> values)
        {
            var index = _random.Next(values.Count);
            if (index < 0 || index >= values.Count)
                throw new InvalidOperationException("The location random source returned an out-of-range index.");
            return values[index];
        }

        /// <summary>
        /// Sets the supplied value.
        /// </summary>
        /// <param name="parameters">The parameters value.</param>
        /// <param name="key">The parameter key to use.</param>
        /// <param name="value">The value to process.</param>
        private static void Set(ParamBag parameters, ParamKey<string> key, string value)
        {
            if (parameters.ContainsKey(key)) parameters.Set(key, value);
            else parameters.Add(key, value);
        }
    }
}
