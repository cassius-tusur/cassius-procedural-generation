using LocationGenerator.Contracts;
using LocationGenerator.Presets;
using LocationGenerator.Strategies;
using Xunit;

namespace LocationGenerator.Tests.Contracts
{
    public class LocationTopologyPresetsTests
    {
        [Fact]
        public void Presets_ReturnFreshAppendRequestsWithDocumentedStructuralSettings()
        {
            var linear = LocationTopologyPresets.LinearAdventure();
            var branching = LocationTopologyPresets.BranchingLair();
            var loop = LocationTopologyPresets.LoopDungeon();
            var settlement = LocationTopologyPresets.Settlement();
            var routes = LocationTopologyPresets.RegionRoutes();

            AssertRequest(linear, "Linear", 5, new LinearOptions());
            AssertRequest(branching, "Branching", 7, new BranchingOptions(3));
            AssertRequest(loop, "Loop", 8, new LoopOptions(1, 3));
            AssertRequest(settlement, "Hub", 7, new HubOptions());
            AssertRequest(routes, "Loop", 8, new LoopOptions(2, 3));
            Assert.NotSame(LocationTopologyPresets.LinearAdventure(), LocationTopologyPresets.LinearAdventure());
        }

        [Fact]
        public void DungeonFlatV1_HasTheSameStructuralSettingsAsLoopDungeon()
        {
            var dungeon = LocationTopologyPresets.DungeonFlatV1();
            var loop = LocationTopologyPresets.LoopDungeon();

            Assert.Equal(loop.StrategyId, dungeon.StrategyId);
            Assert.Equal(loop.NodeCount, dungeon.NodeCount);
            Assert.Equal(loop.Options, dungeon.Options);
            Assert.Equal(LocationGenerationMode.Append, dungeon.Mode);
            Assert.Null(dungeon.AttachmentId);
        }

        [Fact]
        public void PresetRequest_CanBeChangedWithWithoutChangingANewPreset()
        {
            var altered = LocationTopologyPresets.BranchingLair() with { NodeCount = 9 };
            var fresh = LocationTopologyPresets.BranchingLair();

            Assert.Equal(9, altered.NodeCount);
            Assert.Equal(7, fresh.NodeCount);
        }

        private static void AssertRequest(LocationTopologyRequest request, string strategyId, int nodeCount,
            ILocationTopologyOptions options)
        {
            Assert.Equal(strategyId, request.StrategyId);
            Assert.Equal(nodeCount, request.NodeCount);
            Assert.Equal(options, request.Options);
            Assert.Equal(LocationGenerationMode.Append, request.Mode);
            Assert.Null(request.AttachmentId);
        }
    }
}
