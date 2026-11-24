using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Resolves topology strategies by identifier and compatible option type.
    /// </summary>
    /// <seealso cref="ILocationTopologyStrategy"/>
    public sealed class LocationStrategyRegistry
    {
        private readonly IReadOnlyDictionary<string, ILocationTopologyStrategy> _strategies;

        /// <summary>
        /// Initializes a new instance of <see cref="LocationStrategyRegistry"/>.
        /// </summary>
        /// <param name="strategies">The strategies to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        public LocationStrategyRegistry(IEnumerable<ILocationTopologyStrategy> strategies)
        {
            if (strategies == null) throw new ArgumentNullException(nameof(strategies));
            var diagnostics = new List<LocationGenerationDiagnostic>();
            var map = new Dictionary<string, ILocationTopologyStrategy>(StringComparer.Ordinal);
            foreach (var strategy in strategies)
            {
                if (strategy == null)
                {
                    diagnostics.Add(new LocationGenerationDiagnostic("strategy.invalid", "strategies", "must not contain null"));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(strategy.StrategyId))
                {
                    diagnostics.Add(new LocationGenerationDiagnostic("strategy.invalid", "strategyId", "is required"));
                    continue;
                }
                if (strategy.OptionsType == null)
                {
                    diagnostics.Add(new LocationGenerationDiagnostic("strategy.invalid", "optionsType", "is required"));
                    continue;
                }
                if (!map.TryAdd(strategy.StrategyId, strategy))
                    diagnostics.Add(new LocationGenerationDiagnostic("strategy.duplicate", "strategyId", "is registered more than once"));
            }
            if (diagnostics.Count > 0) throw new LocationGenerationValidationException(diagnostics);
            _strategies = map;
        }

        /// <summary>
        /// Resolves the registered strategy that supports the supplied identifier and options.
        /// </summary>
        /// <param name="strategyId">The registered strategy identifier.</param>
        /// <param name="options">The generation options.</param>
        /// <returns>The registered strategy that supports the supplied identifier and options.</returns>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        public ILocationTopologyStrategy Resolve(string strategyId, ILocationTopologyOptions options)
        {
            var diagnostics = new List<LocationGenerationDiagnostic>();
            ILocationTopologyStrategy? strategy = null;
            if (string.IsNullOrWhiteSpace(strategyId) || !_strategies.TryGetValue(strategyId, out strategy))
                diagnostics.Add(new LocationGenerationDiagnostic("strategy.unknown", "strategyId", "is not registered"));
            else if (options == null || !strategy.OptionsType.IsInstanceOfType(options))
                diagnostics.Add(new LocationGenerationDiagnostic("options.typeMismatch", "options", "does not match the strategy"));
            if (diagnostics.Count > 0) throw new LocationGenerationValidationException(diagnostics);
            strategy!.ValidateOptions(options!);
            return strategy;
        }

        /// <summary>
        /// Creates an instance populated with the default configuration.
        /// </summary>
        /// <returns>The resulting instance populated with the default configuration.</returns>
        public static LocationStrategyRegistry CreateDefault()
            => new LocationStrategyRegistry(new ILocationTopologyStrategy[]
            {
                new LinearTopologyStrategy(),
                new BranchingTopologyStrategy(),
                new LoopTopologyStrategy(),
                new HubTopologyStrategy()
            });
    }
}
