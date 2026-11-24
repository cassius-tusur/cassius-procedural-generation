using System;
using LocationGenerator.Contracts;
using LocationGenerator.Strategies;
using Xunit;

namespace LocationGenerator.Tests.Contracts
{
    public class LocationGenerationOptionsTests
    {
        [Fact]
        public void Request_AllowsIndependentTypedOptionsAndMode()
        {
            var options = new LoopOptions(2, 3);
            var request = new LocationTopologyRequest("Loop", 8, options, LocationGenerationMode.Overwrite);
            Assert.Equal("Loop", request.StrategyId);
            Assert.Equal(8, request.NodeCount);
            Assert.Same(options, request.Options);
            Assert.Equal(LocationGenerationMode.Overwrite, request.Mode);
            Assert.Null(request.AttachmentId);
        }

        [Fact]
        public void Result_ReportsOperationObjectsInsteadOfCopiesOfWorldElements()
        {
            var root = new BaseClasses.Model.Element(BaseClasses.Enum.ElemType.Location);
            var created = new[] { new BaseClasses.Model.Element(BaseClasses.Enum.ElemType.Location) };
            var removed = new[] { Guid.NewGuid() };
            var result = new LocationTopologyResult(root, created, null, removed);
            Assert.Same(root, result.Root);
            Assert.Same(created[0], result.CreatedLocations[0]);
            Assert.Equal(removed, result.RemovedElementIds);
        }
    }
}
