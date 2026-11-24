using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Model.Params;
using BaseClasses.Services;
using BaseClasses.Services.Binds;
using LocationGenerator.Contracts;
using LocationGenerator.Generation;
using LocationGenerator.Infrastructure;
using LocationGenerator.Params;
using LocationGenerator.Strategies;
using Xunit;

namespace LocationGenerator.Tests.Generation
{
    public class LocationTopologyPlanValidatorTests
    {
        [Theory]
        [InlineData("unknown endpoint")]
        [InlineData("self loop")]
        [InlineData("duplicate same direction")]
        [InlineData("duplicate reverse")]
        [InlineData("old to old")]
        [InlineData("extra bridge")]
        [InlineData("wrong attachment")]
        [InlineData("disconnected")]
        public void Validate_RejectsMalformedProposalEvenWhenStrategyDoesNothing(string scenario)
        {
            var fixture = scenario == "old to old"
                ? NewAppendFixture(1, oldNodeCount: 2, connectOldNodes: true)
                : NewAppendFixture(scenario is "self loop" or "duplicate same direction" or "duplicate reverse" or "wrong attachment" ? 1 : 2);
            var old = fixture.Old!;
            var first = fixture.NewLocations[0];
            var second = fixture.NewLocations.Last();
            var proposal = scenario switch
            {
                "unknown endpoint" => new LocationStrategyPlan(new[]
                {
                    new LocationPassageProposal(old.Id, first.Id),
                    new LocationPassageProposal(first.Id, second.Id),
                    new LocationPassageProposal(second.Id, Id(999))
                }, old.Id),
                "self loop" => new LocationStrategyPlan(new[]
                {
                    new LocationPassageProposal(old.Id, first.Id),
                    new LocationPassageProposal(first.Id, first.Id)
                }, old.Id),
                "duplicate same direction" => new LocationStrategyPlan(new[]
                {
                    new LocationPassageProposal(old.Id, first.Id),
                    new LocationPassageProposal(old.Id, first.Id)
                }, old.Id),
                "duplicate reverse" => new LocationStrategyPlan(new[]
                {
                    new LocationPassageProposal(old.Id, first.Id),
                    new LocationPassageProposal(first.Id, old.Id)
                }, old.Id),
                "old to old" => new LocationStrategyPlan(
                    new[]
                    {
                        new LocationPassageProposal(old.Id, first.Id),
                        new LocationPassageProposal(old.Id, fixture.Scope.ExistingGraph.NodeIds.Single(id => id != old.Id))
                    }, old.Id),
                "extra bridge" => new LocationStrategyPlan(new[]
                {
                    new LocationPassageProposal(old.Id, first.Id),
                    new LocationPassageProposal(first.Id, second.Id),
                    new LocationPassageProposal(old.Id, second.Id)
                }, old.Id),
                "wrong attachment" => new LocationStrategyPlan(
                    new[] { new LocationPassageProposal(old.Id, first.Id) }, fixture.Root.Id),
                "disconnected" => new LocationStrategyPlan(Array.Empty<LocationPassageProposal>(), old.Id),
                _ => throw new ArgumentOutOfRangeException(nameof(scenario))
            };

            AssertValidationFailure(fixture, proposal, new NoOpStrategy());
        }

        [Fact]
        public void Validate_RejectsWrongNewNodeCountAndLeavesPlotUntouched()
        {
            var fixture = NewAppendFixture(2);
            var proposal = new LocationStrategyPlan(new[]
            {
                new LocationPassageProposal(fixture.Old!.Id, fixture.NewLocations[0].Id),
                new LocationPassageProposal(fixture.NewLocations[0].Id, fixture.NewLocations[1].Id)
            }, fixture.Old!.Id);
            var before = Serializer.SerializeToString(fixture.Plot);

            var exception = Assert.Throws<LocationGenerationValidationException>(() => fixture.Validator.Validate(
                fixture.Plot, fixture.Scope, fixture.Request with { NodeCount = 3 }, fixture.Context, proposal,
                new NoOpStrategy(), fixture.Plan));

            Assert.NotEmpty(exception.Diagnostics);
            Assert.Equal(before, Serializer.SerializeToString(fixture.Plot));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Validate_RejectsNewIdCollisionWithRetainedOrRemovedLocation(bool removedCollision)
        {
            var fixture = NewCollisionFixture(removedCollision);
            var proposal = new LocationStrategyPlan(Array.Empty<LocationPassageProposal>(), null);

            AssertValidationFailure(fixture, proposal, new NoOpStrategy());
        }

        [Fact]
        public void Validate_RejectsPlanReferencingRemovedVertex()
        {
            var fixture = NewOverwriteFixture();
            var removed = Assert.IsAssignableFrom<IElement>(fixture.Removed);
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(removed.Id, fixture.NewLocations[0].Id) }, null);

            var exception = Assert.Throws<LocationGenerationValidationException>(() => fixture.Validator.Validate(
                fixture.Plot, fixture.Scope, fixture.Request, fixture.Context, proposal, new NoOpStrategy(), fixture.Plan));
            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.ElementIds.Contains(removed.Id));
        }

        [Fact]
        public void Validate_RejectsPlanWithMissingReversePassage()
        {
            var fixture = NewAppendFixture(1);
            var old = Assert.IsAssignableFrom<IElement>(fixture.Old);
            fixture.Plan.AddRelation(new Relation(old, fixture.NewLocations[0], BaseRelationKeys.Passage, true));
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old.Id, fixture.NewLocations[0].Id) }, old.Id);

            AssertValidationFailure(fixture, proposal, new NoOpStrategy());
        }

        [Fact]
        public void Validate_RejectsExtraInactivePassageInOtherwiseValidAssembledPlan()
        {
            var fixture = NewAppendFixture(1);
            var old = Assert.IsAssignableFrom<IElement>(fixture.Old);
            fixture.Plan.AddRelation(new Relation(old, fixture.NewLocations[0], BaseRelationKeys.Passage, false));
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old.Id, fixture.NewLocations[0].Id) }, old.Id);

            var exception = Assert.Throws<LocationGenerationValidationException>(() => fixture.Validator.Validate(
                fixture.Plot, fixture.Scope, fixture.Request, fixture.Context, proposal, new NoOpStrategy(), fixture.Plan));

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "plan.passage.unpaired");
        }

        [Fact]
        public void Validate_RejectsNewLocationWithoutActiveRootContains()
        {
            var fixture = NewAppendFixture(1, addContains: false);
            var old = Assert.IsAssignableFrom<IElement>(fixture.Old);
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old.Id, fixture.NewLocations[0].Id) }, old.Id);

            AssertValidationFailure(fixture, proposal, new NoOpStrategy());
        }

        [Fact]
        public void Validate_DoesNotCountFalseRootContainsAsActive()
        {
            var fixture = NewAppendFixture(1, addContains: false);
            var old = Assert.IsAssignableFrom<IElement>(fixture.Old);
            fixture.Plan.AddRelation(new Relation(fixture.Root, fixture.NewLocations[0], BaseRelationKeys.Contains, false));
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old.Id, fixture.NewLocations[0].Id) }, old.Id);

            AssertValidationFailure(fixture, proposal, new NoOpStrategy());
        }

        [Fact]
        public void Validate_RequiresExactlyOneActiveRootContainsForEveryNewLocation()
        {
            var fixture = NewAppendFixture(1);
            var old = Assert.IsAssignableFrom<IElement>(fixture.Old);
            fixture.Plan.AddRelation(new Relation(fixture.Root, fixture.NewLocations[0], BaseRelationKeys.Contains, true));
            fixture.Plan.AddRelation(new Relation(fixture.Root, fixture.NewLocations[0], BaseRelationKeys.Contains, true));
            var proposal = new LocationStrategyPlan(
                new[] { new LocationPassageProposal(old.Id, fixture.NewLocations[0].Id) }, old.Id);

            AssertValidationFailure(fixture, proposal, new NoOpStrategy());
        }

        [Fact]
        public void Validate_DelegatesBuiltinShapeViolationToStrategyAfterCommonChecks()
        {
            var fixture = NewFreshFixture(3);
            var proposal = new LocationStrategyPlan(new[]
            {
                new LocationPassageProposal(fixture.NewLocations[0].Id, fixture.NewLocations[1].Id),
                new LocationPassageProposal(fixture.NewLocations[1].Id, fixture.NewLocations[2].Id),
                new LocationPassageProposal(fixture.NewLocations[2].Id, fixture.NewLocations[0].Id)
            }, null);

            var exception = Assert.Throws<LocationGenerationValidationException>(() => fixture.Validator.Validate(
                fixture.Plot, fixture.Scope, fixture.Request with { Options = new LinearOptions() }, fixture.Context, proposal,
                new LinearTopologyStrategy(), fixture.Plan));

            Assert.Contains(exception.Diagnostics, diagnostic => diagnostic.Code == "graph.incompatible");
        }

        private static void AssertValidationFailure(Fixture fixture, LocationStrategyPlan proposal,
            ILocationTopologyStrategy strategy)
        {
            var exception = Assert.Throws<LocationGenerationValidationException>(() => fixture.Validator.Validate(
                fixture.Plot, fixture.Scope, fixture.Request, fixture.Context, proposal, strategy, fixture.Plan));
            Assert.NotEmpty(exception.Diagnostics);
        }

        private static Fixture NewAppendFixture(int newNodeCount, int oldNodeCount = 1, bool connectOldNodes = false,
            bool addContains = true)
        {
            var plot = new Plot();
            var root = NewLocation();
            var oldLocations = Enumerable.Range(1, oldNodeCount).Select(_ => NewLocation()).ToArray();
            var old = oldLocations[0];
            plot.Add(root);
            foreach (var location in oldLocations) plot.Add(location);
            var existingPassages = connectOldNodes
                ? oldLocations.Skip(1).Select(location => new LocationPassageProposal(old.Id, location.Id)).ToArray()
                : Array.Empty<LocationPassageProposal>();
            var scope = new LocationGenerationScope(root,
                new LocationGraphSnapshot(oldLocations.Select(location => location.Id).ToArray(), existingPassages),
                oldLocations.ToDictionary(location => location.Id, location => (IElement)location), Array.Empty<IElement>(), Array.Empty<Relation>());
            return NewFixture(plot, root, old, null, scope, newNodeCount, LocationGenerationMode.Append, old.Id,
                addContains);
        }

        private static Fixture NewFreshFixture(int newNodeCount)
        {
            var plot = new Plot();
            var root = NewLocation();
            plot.Add(root);
            var scope = new LocationGenerationScope(root,
                new LocationGraphSnapshot(Array.Empty<Guid>(), Array.Empty<LocationPassageProposal>()),
                new Dictionary<Guid, IElement>(), Array.Empty<IElement>(), Array.Empty<Relation>());
            return NewFixture(plot, root, null, null, scope, newNodeCount, LocationGenerationMode.Append, null);
        }

        private static Fixture NewOverwriteFixture()
        {
            var plot = new Plot();
            var root = NewLocation();
            var removed = NewLocation();
            plot.Add(root);
            plot.Add(removed);
            var scope = new LocationGenerationScope(root,
                new LocationGraphSnapshot(Array.Empty<Guid>(), Array.Empty<LocationPassageProposal>()),
                new Dictionary<Guid, IElement>(), new[] { removed }, Array.Empty<Relation>());
            return NewFixture(plot, root, null, removed, scope, 1, LocationGenerationMode.Overwrite, null);
        }

        private static Fixture NewCollisionFixture(bool withRemovedLocation)
        {
            var plot = new Plot();
            var root = NewLocation();
            var colliding = NewLocation();
            plot.Add(root);
            plot.Add(colliding);
            var removed = withRemovedLocation ? colliding : null;
            var scope = new LocationGenerationScope(root,
                new LocationGraphSnapshot(Array.Empty<Guid>(), Array.Empty<LocationPassageProposal>()),
                new Dictionary<Guid, IElement>(), removed == null ? Array.Empty<IElement>() : new[] { removed },
                Array.Empty<Relation>());
            var plan = new LocationMutationPlan();
            var duplicate = new ElementWithId(ElemType.Location, colliding.Id);
            plan.AddLocation(duplicate);
            plan.AddRelation(new Relation(root, duplicate, BaseRelationKeys.Contains, true));
            if (removed != null) plan.RemoveLocation(removed);
            var request = new LocationTopologyRequest("test", 1, new TestOptions(),
                withRemovedLocation ? LocationGenerationMode.Overwrite : LocationGenerationMode.Append);
            var context = new LocationStrategyContext(scope.ExistingGraph, new[] { duplicate.Id }, null, new ZeroRandom());
            return new Fixture(new LocationTopologyPlanValidator(), plot, root, null, removed, scope, request,
                context, plan, new IElement[] { duplicate });
        }

        private static Fixture NewFixture(Plot plot, IElement root, IElement? old, IElement? removed,
            LocationGenerationScope scope, int count, LocationGenerationMode mode, Guid? attachment, bool addContains = true)
        {
            var plan = new LocationMutationPlan();
            var additions = Enumerable.Range(1, count).Select(index => NewLocation()).ToArray();
            foreach (var addition in additions)
            {
                plan.AddLocation(addition);
                if (addContains)
                    plan.AddRelation(new Relation(root, addition, BaseRelationKeys.Contains, true));
            }
            if (removed != null) plan.RemoveLocation(removed);
            var request = new LocationTopologyRequest("test", count, new TestOptions(), mode, attachment);
            var context = new LocationStrategyContext(scope.ExistingGraph, additions.Select(location => location.Id).ToArray(),
                attachment, new ZeroRandom());
            return new Fixture(new LocationTopologyPlanValidator(), plot, root, old, removed, scope, request,
                context, plan, additions);
        }

        private static Element NewLocation() => new Element(ElemType.Location);
        private static Guid Id(int value) => Guid.Parse($"00000000-0000-0000-0000-{value:D12}");

        private sealed record Fixture(LocationTopologyPlanValidator Validator, Plot Plot, IElement Root,
            IElement? Old, IElement? Removed, LocationGenerationScope Scope, LocationTopologyRequest Request,
            LocationStrategyContext Context, LocationMutationPlan Plan, IElement[] NewLocations);

        private sealed class TestOptions : ILocationTopologyOptions { }

        private sealed class NoOpStrategy : ILocationTopologyStrategy
        {
            public string StrategyId => "test";
            public Type OptionsType => typeof(TestOptions);
            public void ValidateOptions(ILocationTopologyOptions options) { }
            public LocationStrategyPlan Build(LocationStrategyContext context, ILocationTopologyOptions options) =>
                new LocationStrategyPlan(Array.Empty<LocationPassageProposal>(), context.AttachmentId);
            public void ValidatePlan(LocationStrategyContext context, ILocationTopologyOptions options,
                LocationStrategyPlan plan) { }
        }

        private sealed class ZeroRandom : ILocationRandom
        {
            public int Next(int maxExclusive) => 0;
        }

        // The production validator must compare Ids, not object identity. This test fixture needs a collision.
        private sealed class ElementWithId : IElement
        {
            public ElementWithId(ElemType type, Guid id)
            {
                Type = type;
                Id = id;
                Params = new BaseClasses.Model.Params.ParamBag();
            }

            public Guid Id { get; }
            public ElemType Type { get; }
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public BaseClasses.Model.Params.ParamBag Params { get; }
            public int Time { get; set; } = -1;
            public bool IsEmpty() => false;
        }
    }
}
