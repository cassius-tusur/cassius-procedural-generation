using System;
using System.Collections.Generic;
using BaseClasses.Enum;
using BaseClasses.Model;
using LocationGenerator.Contracts;
using LocationGenerator.Strategies;
using Xunit;

namespace LocationGenerator.Tests.Contracts
{
    public class LocationTopologyRequestTests
    {
        [Fact]
        public void Request_StoresStrategyNodeCountOptionsModeAndAttachment()
        {
            var options = new LinearOptions();
            var attachment = Guid.NewGuid();
            var request = new LocationTopologyRequest("Linear", 3, options,
                LocationGenerationMode.Append, attachment);

            Assert.Equal("Linear", request.StrategyId);
            Assert.Equal(3, request.NodeCount);
            Assert.Same(options, request.Options);
            Assert.Equal(LocationGenerationMode.Append, request.Mode);
            Assert.Equal(attachment, request.AttachmentId);
        }

        [Fact]
        public void ResultAndDiagnostic_CopyInputCollections()
        {
            var ids = new List<Guid> { Guid.NewGuid() };
            var relations = new List<LocationRelationReference>
            {
                new LocationRelationReference(ids[0], Guid.NewGuid(), "world", "link")
            };
            var root = new Element(ElemType.Location);
            var created = new List<BaseClasses.Interface.IElement> { root };
            var removed = new List<Guid> { Guid.NewGuid() };
            var diagnostic = new LocationGenerationDiagnostic("code", "path", "message", ids, relations);
            var result = new LocationTopologyResult(root, created, root, removed);

            ids.Clear();
            relations.Clear();
            created.Clear();
            removed.Clear();

            Assert.Single(diagnostic.ElementIds);
            Assert.Single(diagnostic.Relations);
            Assert.Single(result.CreatedLocations);
            Assert.Single(result.RemovedElementIds);
        }
    }
}
