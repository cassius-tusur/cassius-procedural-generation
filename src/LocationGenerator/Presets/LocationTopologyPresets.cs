using LocationGenerator.Contracts;
using LocationGenerator.Strategies;

namespace LocationGenerator.Presets
{
    /// <summary>
    /// Named topology requests used by the public examples and migration guide.
    /// Each factory returns a new append request so callers can safely customize
    /// it with a record <c>with</c> expression.
    /// </summary>
    public static class LocationTopologyPresets
    {
        /// <summary>
        /// Creates the predefined linear-adventure topology request.
        /// </summary>
        /// <returns>The predefined linear-adventure topology request.</returns>
        public static LocationTopologyRequest LinearAdventure() =>
            Append("Linear", 5, new LinearOptions());

        /// <summary>
        /// Creates the predefined branching-lair topology request.
        /// </summary>
        /// <returns>The predefined branching-lair topology request.</returns>
        public static LocationTopologyRequest BranchingLair() =>
            Append("Branching", 7, new BranchingOptions(3));

        /// <summary>
        /// Creates the predefined loop-dungeon topology request.
        /// </summary>
        /// <returns>The predefined loop-dungeon topology request.</returns>
        public static LocationTopologyRequest LoopDungeon() =>
            Append("Loop", 8, new LoopOptions(1, 3));

        /// <summary>
        /// Creates the predefined settlement topology request.
        /// </summary>
        /// <returns>The predefined settlement topology request.</returns>
        public static LocationTopologyRequest Settlement() =>
            Append("Hub", 7, new HubOptions());

        /// <summary>
        /// Creates the predefined region-routes topology request.
        /// </summary>
        /// <returns>The predefined region-routes topology request.</returns>
        public static LocationTopologyRequest RegionRoutes() =>
            Append("Loop", 8, new LoopOptions(2, 3));

        /// <summary>
        /// Compatibility name for the former DungeonFlatV1 topology. The topology is the same as <see cref="LoopDungeon"/>; attribute population remains a separate concern.
        /// </summary>
        /// <returns>A fresh request equivalent to <see cref="LoopDungeon"/>.</returns>
        public static LocationTopologyRequest DungeonFlatV1() =>
            LoopDungeon();

        /// <summary>
        /// Creates an append-mode topology request for the specified strategy and options.
        /// </summary>
        /// <param name="strategyId">The registered strategy identifier.</param>
        /// <param name="nodeCount">The node count value.</param>
        /// <param name="options">The generation options.</param>
        /// <returns>The resulting append-mode topology request for the specified strategy and options.</returns>
        private static LocationTopologyRequest Append(string strategyId, int nodeCount,
            ILocationTopologyOptions options) =>
            new LocationTopologyRequest(strategyId, nodeCount, options, LocationGenerationMode.Append);
    }
}
