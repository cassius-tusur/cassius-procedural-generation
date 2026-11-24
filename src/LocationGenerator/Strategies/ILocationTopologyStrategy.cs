using System;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Defines a strategy that builds and validates location-topology plans.
    /// </summary>
    public interface ILocationTopologyStrategy
    {
        string StrategyId { get; }
        Type OptionsType { get; }
        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="options">The generation options.</param>
        void ValidateOptions(ILocationTopologyOptions options);
        /// <summary>
        /// Builds a topology plan from the supplied context and options.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="options">The generation options.</param>
        /// <returns>The topology plan proposed by the strategy.</returns>
        LocationStrategyPlan Build(LocationStrategyContext context, ILocationTopologyOptions options);
        /// <summary>
        /// Validates the plan.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="options">The generation options.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        void ValidatePlan(LocationStrategyContext context, ILocationTopologyOptions options,
            LocationStrategyPlan plan);
    }
}
