using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Services;
using LocationGenerator.Contracts;
using LocationGenerator.Generation;
using LocationGenerator.Params;
using LocationGenerator.Queries;
using LocationGenerator.Strategies;
using LocationGenerator.Tests.Support;
using Xunit;

namespace LocationGenerator.Tests.Generation
{
    public class LocationTopologyGeneratorTests
    {
        public static IEnumerable<object[]> Algorithms()
        {
            yield return new object[] { new LocationTopologyRequest("Linear", 3, new LinearOptions(), LocationGenerationMode.Append) };
            yield return new object[] { new LocationTopologyRequest("Branching", 4, new BranchingOptions(3), LocationGenerationMode.Append) };
            yield return new object[] { new LocationTopologyRequest("Loop", 3, new LoopOptions(1, 2), LocationGenerationMode.Append) };
            yield return new object[] { new LocationTopologyRequest("Hub", 4, new HubOptions(), LocationGenerationMode.Append) };
        }

        [Theory]
        [MemberData(nameof(Algorithms))]
        public void Generate_ParameterlessRoot_CreatesExactPlainDirectChildren(LocationTopologyRequest request)
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var result = NewGenerator(request).Generate(plot, root, request);
            var children = LocationTopology.GetChildren(plot, root);

            Assert.Same(root, result.Root);
            Assert.Equal(request.NodeCount, children.Count);
            Assert.Equal(children.Select(x => x.Id), result.CreatedLocations.Select(x => x.Id));
            Assert.Null(result.Attachment);
            Assert.Empty(result.RemovedElementIds);
            Assert.All(children, child =>
            {
                Assert.Contains(child, plot.Elements);
                Assert.Equal(string.Empty, child.Name);
                Assert.Equal(string.Empty, child.Description);
                Assert.False(child.Params.ContainsKey(LocationParams.Biome));
            });
            Assert.DoesNotContain(plot.Relations, x => x.Param.Equals(LocationRelationKeys.Entrance));
        }

        [Fact]
        public void Generate_Append_PreservesExistingObjectsAndAddsExactChildren()
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var old = AddLocation(plot);
            plot.Relations.Add(new Relation(root, old, BaseClasses.Services.Binds.BaseRelationKeys.Contains, true));
            var request = new LocationTopologyRequest("Linear", 2, new LinearOptions(), LocationGenerationMode.Append);

            var result = NewGenerator(request).Generate(plot, root, request);

            Assert.Same(old, plot.Elements.Single(x => x.Id == old.Id));
            Assert.Equal(3, LocationTopology.GetChildren(plot, root).Count);
            Assert.Same(old, result.Attachment);
            Assert.Empty(result.RemovedElementIds);
        }

        [Fact]
        public void Generate_Overwrite_RemovesAllowedContainsSubtreeAndReportsActualIds()
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var old = AddLocation(plot);
            var grandchild = AddLocation(plot);
            plot.Relations.Add(new Relation(root, old, BaseClasses.Services.Binds.BaseRelationKeys.Contains, true));
            plot.Relations.Add(new Relation(old, grandchild, BaseClasses.Services.Binds.BaseRelationKeys.Contains, true));
            var request = new LocationTopologyRequest("Linear", 2, new LinearOptions(), LocationGenerationMode.Overwrite);

            var result = NewGenerator(request).Generate(plot, root, request);

            Assert.Equal(new[] { old.Id, grandchild.Id }.OrderBy(x => x), result.RemovedElementIds.OrderBy(x => x));
            Assert.DoesNotContain(plot.Elements, x => x.Id == old.Id || x.Id == grandchild.Id);
            Assert.All(result.CreatedLocations, x => Assert.Same(x, plot.Elements.Single(y => y.Id == x.Id)));
            Assert.Equal(2, LocationTopology.GetChildren(plot, root).Count);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public void GenerateChain_UsesConstructorRequestAndRunsAtMostOneLevel(int recursion)
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var request = new LocationTopologyRequest("Linear", 2, new LinearOptions(), LocationGenerationMode.Append);
            IChainGenerator generator = NewGenerator(request);
            if (recursion < 0)
            {
                AssertDiagnostic(Assert.Throws<LocationGenerationValidationException>(() => generator.GenerateChain(plot, root, recursion)), "recursion.invalid", "recursion");
                return;
            }

            var result = generator.GenerateChain(plot, root, recursion);
            Assert.Same(root, result);
            Assert.Equal(recursion == 0 ? 0 : request.NodeCount, LocationTopology.GetChildren(plot, root).Count);
            Assert.All(LocationTopology.GetChildren(plot, root), child => Assert.Empty(LocationTopology.GetChildren(plot, child)));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        public async Task GenerateChainAsync_UsesTheSameSingleLevelRecursionContract(int recursion)
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            IChainGenerator generator = NewGenerator(ValidRequest());
            var task = generator.GenerateChainAsync(plot, root, recursion);

            if (recursion < 0)
            {
                AssertDiagnostic(await Assert.ThrowsAsync<LocationGenerationValidationException>(() => task), "recursion.invalid", "recursion");
                return;
            }

            Assert.Same(root, await task);
            Assert.Equal(recursion == 0 ? 0 : 1, LocationTopology.GetChildren(plot, root).Count);
        }

        [Fact]
        public void GenerateChain_Zero_ValidatesRootButSkipsMalformedLocalGraphAndRandom()
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var child = AddLocation(plot);
            plot.Relations.Add(new Relation(root, child, BaseClasses.Services.Binds.BaseRelationKeys.Contains, true));
            plot.Relations.Add(new Relation(child, child, BaseClasses.Services.Binds.BaseRelationKeys.Passage, true));
            IChainGenerator generator = new LocationTopologyGenerator(ValidRequest(), LocationStrategyRegistry.CreateDefault(), new ThrowingRandom());
            var before = Serializer.SerializeToString(plot);

            Assert.Same(root, generator.GenerateChain(plot, root, 0));
            Assert.Equal(before, Serializer.SerializeToString(plot));
        }

        [Fact]
        public void GenerateChain_Zero_DoesNotResolveTheConfiguredStrategy()
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var invalidOnlyWhenRun = new LocationTopologyRequest("not-registered", 0, new LinearOptions(), LocationGenerationMode.Append);
            IChainGenerator generator = new LocationTopologyGenerator(invalidOnlyWhenRun,
                LocationStrategyRegistry.CreateDefault(), new ThrowingRandom());

            Assert.Same(root, generator.GenerateChain(plot, root, 0));
            Assert.Empty(LocationTopology.GetChildren(plot, root));
        }

        [Fact]
        public async Task GenerateChainAsync_Zero_SkipsMalformedGraphAndThrowingRandom()
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var child = AddLocation(plot);
            plot.Relations.Add(new Relation(root, child, BaseClasses.Services.Binds.BaseRelationKeys.Contains, true));
            plot.Relations.Add(new Relation(child, child, BaseClasses.Services.Binds.BaseRelationKeys.Passage, true));
            IChainGenerator generator = new LocationTopologyGenerator(ValidRequest(),
                LocationStrategyRegistry.CreateDefault(), new ThrowingRandom());
            var before = Serializer.SerializeToString(plot);

            Assert.Same(root, await generator.GenerateChainAsync(plot, root, 0));
            Assert.Equal(before, Serializer.SerializeToString(plot));
        }

        [Fact]
        public async Task GenerateChainAsync_Zero_DoesNotResolveUnregisteredConfiguredStrategy()
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var request = new LocationTopologyRequest("not-registered", 0, new LinearOptions(), LocationGenerationMode.Append);
            IChainGenerator generator = new LocationTopologyGenerator(request,
                LocationStrategyRegistry.CreateDefault(), new ThrowingRandom());

            Assert.Same(root, await generator.GenerateChainAsync(plot, root, 0));
            Assert.Empty(LocationTopology.GetChildren(plot, root));
        }

        [Fact]
        public void GenerateChain_Zero_RejectsNullOutsideAndNonLocationRoots()
        {
            IChainGenerator generator = NewGenerator(ValidRequest());
            var plot = new Plot();
            var outside = new Element(ElemType.Location);
            var item = new Element(ElemType.Item);
            plot.Add(item);

            Assert.Throws<ArgumentNullException>(() => generator.GenerateChain(plot, null!, 0));
            AssertDiagnosticAt(Assert.Throws<LocationGenerationValidationException>(() => generator.GenerateChain(plot, outside, 0)), "root");
            AssertDiagnosticAt(Assert.Throws<LocationGenerationValidationException>(() => generator.GenerateChain(plot, item, 0)), "root.type");
        }

        [Fact]
        public async Task GenerateChainAsync_Zero_StillFaultsForInvalidRootArguments()
        {
            IChainGenerator generator = NewGenerator(ValidRequest());
            var plot = new Plot();
            var outside = new Element(ElemType.Location);
            var item = new Element(ElemType.Item);
            plot.Add(item);

            await Assert.ThrowsAsync<ArgumentNullException>(() => generator.GenerateChainAsync(plot, null!, 0));
            AssertDiagnosticAt(await Assert.ThrowsAsync<LocationGenerationValidationException>(() => generator.GenerateChainAsync(plot, outside, 0)), "root");
            AssertDiagnosticAt(await Assert.ThrowsAsync<LocationGenerationValidationException>(() => generator.GenerateChainAsync(plot, item, 0)), "root.type");
        }

        [Fact]
        public void Generate_ExplicitRequestDoesNotReplaceConstructorRequest()
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var configured = ValidRequest();
            var explicitRequest = new LocationTopologyRequest("Hub", 3, new HubOptions(), LocationGenerationMode.Append);
            var generator = NewGenerator(configured);

            generator.Generate(plot, root, explicitRequest);
            var nested = AddLocation(plot);
            generator.GenerateChain(plot, nested);

            Assert.Single(LocationTopology.GetChildren(plot, nested));
        }

        [Theory]
        [MemberData(nameof(InvalidRequests))]
        public void Generate_InvalidRequest_UsesStableValidationDiagnostic(LocationTopologyRequest request, string? code, string path)
        {
            var plot = new Plot();
            var root = AddRoot(plot);
            var before = Serializer.SerializeToString(plot);
            var error = Assert.Throws<LocationGenerationValidationException>(() => NewGenerator(ValidRequest()).Generate(plot, root, request));
            if (code == null) AssertDiagnosticAt(error, path);
            else AssertDiagnostic(error, code, path);
            Assert.Equal(before, Serializer.SerializeToString(plot));
        }

        public static IEnumerable<object[]> InvalidRequests()
        {
            yield return new object[] { new LocationTopologyRequest("Linear", 0, new LinearOptions(), LocationGenerationMode.Append), null!, "nodeCount" };
            yield return new object[] { new LocationTopologyRequest("Linear", -1, new LinearOptions(), LocationGenerationMode.Append), null!, "nodeCount" };
            yield return new object[] { new LocationTopologyRequest("Missing", 1, new LinearOptions(), LocationGenerationMode.Append), "strategy.unknown", "strategyId" };
            yield return new object[] { new LocationTopologyRequest("Linear", 1, new HubOptions(), LocationGenerationMode.Append), "options.typeMismatch", "options" };
            yield return new object[] { new LocationTopologyRequest("Linear", 1, new LinearOptions(), (LocationGenerationMode)99), null!, "mode" };
            yield return new object[] { new LocationTopologyRequest("Linear", 1, new LinearOptions(), LocationGenerationMode.Append, Guid.NewGuid()), "attachment.unavailable", "attachmentId" };
            yield return new object[] { new LocationTopologyRequest("Linear", 1, new LinearOptions(), LocationGenerationMode.Overwrite, Guid.NewGuid()), "attachment.unavailable", "attachmentId" };
        }

        [Fact]
        public void Generate_NullRequest_UsesValidationExceptionWithRequestPath()
        {
            var plot = new Plot();
            var root = AddRoot(plot);

            AssertDiagnosticAt(Assert.Throws<LocationGenerationValidationException>(() =>
                NewGenerator(ValidRequest()).Generate(plot, root, null!)), "request");
        }

        [Fact]
        public void Constructor_RejectsRequiredDependencies()
        {
            var request = ValidRequest();
            var registry = LocationStrategyRegistry.CreateDefault();
            var random = new SequenceRandom();
            Assert.Throws<ArgumentNullException>(() => new LocationTopologyGenerator(null!, registry, random));
            Assert.Throws<ArgumentNullException>(() => new LocationTopologyGenerator(request, null!, random));
            Assert.Throws<ArgumentNullException>(() => new LocationTopologyGenerator(request, registry, null!));
        }

        private static LocationTopologyRequest ValidRequest() => new LocationTopologyRequest("Linear", 1, new LinearOptions(), LocationGenerationMode.Append);
        private static LocationTopologyGenerator NewGenerator(LocationTopologyRequest request) => new LocationTopologyGenerator(request, LocationStrategyRegistry.CreateDefault(), new SequenceRandom());
        private static Element AddRoot(Plot plot) => AddLocation(plot);
        private static Element AddLocation(Plot plot) { var location = new Element(ElemType.Location); plot.Add(location); return location; }
        private static void AssertDiagnostic(LocationGenerationValidationException error, string code, string path) => Assert.Contains(error.Diagnostics, x => x.Code == code && x.Path == path);
        private static void AssertDiagnosticAt(LocationGenerationValidationException error, string path) =>
            Assert.Contains(error.Diagnostics, x => x.Path == path && !string.IsNullOrWhiteSpace(x.Code));
        private sealed class ThrowingRandom : ILocationRandom { public int Next(int exclusiveMax) => throw new InvalidOperationException("must not be called"); }
    }
}
