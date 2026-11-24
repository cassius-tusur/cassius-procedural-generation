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
using LocationGenerator.Params;
using LocationGenerator.Strategies;
using Xunit;

namespace LocationGenerator.Tests.Generation
{
    public class LocationPlanCommitterTests
    {
        [Fact]
        public void Commit_WhenBinderThrows_RestoresPlotRootParametersAndTime()
        {
            var failingKey = new ParamKey<bool>("Fail", "Test");
            var binder = new Binder();
            binder.Register(
                ElemType.Location,
                ElemType.Location,
                failingKey,
                (_, _, _, _, _) => throw new InvalidOperationException("bind failed"),
                (_, _, _, _, _) => { });
            var plot = new Plot(binder) { Time = 42 };
            var root = NewLocation();
            var oldChild = NewLocation();
            var oldGrandchild = NewLocation();
            var outside = NewLocation();
            root.Params.Add(new ParamKey<string>("Marker", "Test"), "original");
            Add(plot, root, oldChild, oldGrandchild, outside);
            Relate(plot, root, oldChild, BaseRelationKeys.Contains);
            Relate(plot, oldChild, oldGrandchild, BaseRelationKeys.Contains);
            Relate(plot, outside, root, BaseRelationKeys.Contains);
            var plan = new LocationMutationPlan();
            plan.RemoveScope(new LocationGraphInspector().Inspect(plot, root, LocationGenerationMode.Overwrite));
            var newChild = NewLocation();
            plan.AddLocation(newChild);
            plan.AddRelation(new Relation(root, newChild, failingKey, true));
            var before = Serializer.SerializeToString(plot);
            var timeBefore = plot.Time;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new LocationPlanCommitter().Commit(plot, plan));

            Assert.Equal("bind failed", exception.Message);
            Assert.Equal(before, Serializer.SerializeToString(plot));
            Assert.Equal(timeBefore, plot.Time);
            Assert.DoesNotContain(newChild, plot.Elements);
        }

        [Fact]
        public void InspectOverwrite_RemovesOnlyDescendantsOfRoot()
        {
            var plot = new Plot();
            var root = NewLocation();
            var child = NewLocation();
            var grandchild = NewLocation();
            var unrelated = NewLocation();
            var foreignParent = NewLocation();
            Add(plot, root, child, grandchild, unrelated, foreignParent);
            Relate(plot, root, child, BaseRelationKeys.Contains);
            Relate(plot, child, grandchild, BaseRelationKeys.Contains);
            Relate(plot, foreignParent, root, BaseRelationKeys.Contains);
            Relate(plot, foreignParent, unrelated, BaseRelationKeys.Contains);

            var scope = new LocationGraphInspector().Inspect(plot, root, LocationGenerationMode.Overwrite);
            var plan = new LocationMutationPlan();
            plan.RemoveScope(scope);

            Assert.Equal(new[] { child, grandchild }, plan.RemovedLocations);
            Assert.DoesNotContain(root, plan.RemovedLocations);
            Assert.DoesNotContain(unrelated, plan.RemovedLocations);
            Assert.DoesNotContain(foreignParent, plan.RemovedLocations);
        }

        [Fact]
        public void Commit_WhenBinderMutatesRootBeforeThrowing_RestoresRootParametersAndTime()
        {
            var failingKey = new ParamKey<bool>("Fail", "Test");
            var rootMarker = new ParamKey<string>("Marker", "Test");
            var root = NewLocation();
            root.Params.Add(rootMarker, "original");
            var binder = new Binder();
            binder.Register(
                ElemType.Location,
                ElemType.Location,
                failingKey,
                (_, _, _, _, currentPlot) =>
                {
                    root.Params.Set(rootMarker, "binder changed");
                    currentPlot.Time = 999;
                    throw new InvalidOperationException("bind failed");
                },
                (_, _, _, _, _) => { });
            var plot = new Plot(binder);
            var oldChild = NewLocation();
            Add(plot, root, oldChild);
            Relate(plot, root, oldChild, BaseClasses.Services.Binds.BaseRelationKeys.Contains);
            var plan = new LocationMutationPlan();
            plan.RemoveScope(new LocationGraphInspector().Inspect(plot, root, LocationGenerationMode.Overwrite));
            var newChild = NewLocation();
            plan.AddLocation(newChild);
            plan.AddRelation(new Relation(root, newChild, failingKey, true));
            var before = Serializer.SerializeToString(plot);

            Assert.Throws<InvalidOperationException>(() => new LocationPlanCommitter().Commit(plot, plan));

            Assert.Equal(before, Serializer.SerializeToString(plot));
        }

        [Fact]
        public void Commit_WhenBinderMutatesExistingObjectsBeforeThrowing_RestoresCompletePlotState()
        {
            var failingKey = new ParamKey<bool>("Fail", "Test");
            var root = new Element(ElemType.Location, "original name", "original description");
            var oldChild = NewLocation();
            var outside = NewLocation();
            var preservedRelation = new Relation(root, outside, BaseClasses.Services.Binds.BaseRelationKeys.Passage, true);
            var binder = new Binder();
            binder.Register(
                ElemType.Location,
                ElemType.Location,
                failingKey,
                (_, _, _, _, _) =>
                {
                    root.Name = "binder name";
                    root.Description = "binder description";
                    root.Time = 999;
                    preservedRelation.Value = false;
                    throw new InvalidOperationException("bind failed");
                },
                (_, _, _, _, _) => { });
            var plot = new Plot(binder);
            Add(plot, root, oldChild, outside);
            Relate(plot, root, oldChild, BaseClasses.Services.Binds.BaseRelationKeys.Contains);
            plot.Relations.Add(preservedRelation);
            var plan = new LocationMutationPlan();
            plan.RemoveScope(new LocationGraphInspector().Inspect(plot, root, LocationGenerationMode.Overwrite));
            var newChild = NewLocation();
            plan.AddLocation(newChild);
            plan.AddRelation(new Relation(root, newChild, failingKey, true));
            var before = Serializer.SerializeToString(plot);

            Assert.Throws<InvalidOperationException>(() => new LocationPlanCommitter().Commit(plot, plan));

            Assert.Equal(before, Serializer.SerializeToString(plot));
        }

        [Fact]
        public void Commit_WhenRemovalThrows_RestoresSerializedStateOrderReferencesAndCommitException()
        {
            var removalKey = new ParamKey<bool>("Removal", "Test");
            var marker = new ParamKey<string>("Marker", "Test");
            var binder = new Binder();
            var plot = new Plot(binder) { Time = 71 };
            var root = new Element(ElemType.Location, "root", "root description");
            var removed = new Element(ElemType.Location, "removed", "removed description");
            var outside = new Element(ElemType.Location, "outside", "outside description");
            root.Params.Add(marker, "original");
            var preserved = new Relation(root, outside, BaseClasses.Services.Binds.BaseRelationKeys.Passage, true);
            Add(plot, root, removed, outside);
            var removalRelation = new Relation(root, removed, removalKey, true);
            plot.Relations.Add(removalRelation);
            plot.Relations.Add(preserved);
            binder.Register(ElemType.Location, ElemType.Location, removalKey,
                (_, _, _, _, _) => { },
                (_, _, _, _, currentPlot) =>
                {
                    root.Name = "changed while removing";
                    root.Description = "changed description";
                    root.Time = 999;
                    root.Params.Set(marker, "changed");
                    preserved.Value = false;
                    currentPlot.Time = 999;
                    throw new InvalidOperationException("remove failed");
                });
            var plan = new LocationMutationPlan();
            plan.RemoveLocation(removed);
            var before = PlotState.Capture(plot);

            var exception = Assert.Throws<InvalidOperationException>(() => new LocationPlanCommitter().Commit(plot, plan));

            Assert.Equal("remove failed", exception.Message);
            before.AssertRestored(plot);
        }

        [Fact]
        public void Commit_WhenSecondPassageWriteThrows_RestoresSerializedStateOrderReferencesAndCommitException()
        {
            var binder = new Binder();
            var plot = new Plot(binder) { Time = 31 };
            var root = new Element(ElemType.Location, "root", "root description");
            var outside = new Element(ElemType.Location, "outside", "outside description");
            var marker = new ParamKey<string>("Marker", "Test");
            root.Params.Add(marker, "original");
            var preserved = new Relation(root, outside, BaseClasses.Services.Binds.BaseRelationKeys.Contains, true);
            Add(plot, root, outside);
            plot.Relations.Add(preserved);
            var passageWrites = 0;
            binder.Register(ElemType.Location, ElemType.Location, BaseClasses.Services.Binds.BaseRelationKeys.Passage,
                (source, target, key, value, currentPlot) =>
                {
                    passageWrites++;
                    if (passageWrites == 1)
                    {
                        currentPlot.Relations.Add(new Relation(source, target, key, value));
                        return;
                    }
                    root.Name = "changed after first passage";
                    root.Description = "changed description";
                    root.Time = 999;
                    root.Params.Set(marker, "changed");
                    preserved.Value = false;
                    currentPlot.Time = 999;
                    throw new InvalidOperationException("second passage failed");
                },
                (_, _, _, _, _) => { });
            var first = NewLocation();
            var second = NewLocation();
            var plan = new LocationMutationPlan();
            plan.AddLocation(first);
            plan.AddLocation(second);
            plan.AddPassage(first, second, BaseClasses.Services.Binds.BaseRelationKeys.Passage);
            var before = PlotState.Capture(plot);

            var exception = Assert.Throws<InvalidOperationException>(() => new LocationPlanCommitter().Commit(plot, plan));

            Assert.Equal("second passage failed", exception.Message);
            Assert.Equal(2, passageWrites);
            before.AssertRestored(plot);
            Assert.DoesNotContain(first, plot.Elements);
            Assert.DoesNotContain(second, plot.Elements);
        }

        [Fact]
        public void Commit_RemoveScopeRemovesInactiveContainsAndCustomInternalRelation()
        {
            var customKey = new ParamKey<bool>("Internal", "Test");
            var plot = new Plot();
            var root = NewLocation();
            var removed = NewLocation();
            Add(plot, root, removed);
            var inactiveContains = new Relation(root, removed, BaseClasses.Services.Binds.BaseRelationKeys.Contains, false);
            var customInternal = new Relation(removed, removed, customKey, true);
            plot.Relations.Add(inactiveContains);
            plot.Relations.Add(customInternal);
            var scope = new LocationGenerationScope(root,
                new LocationGraphSnapshot(Array.Empty<Guid>(), Array.Empty<LocationPassageProposal>()),
                new Dictionary<Guid, IElement>(), new[] { removed }, new[] { inactiveContains, customInternal });
            var plan = new LocationMutationPlan();
            plan.RemoveScope(scope);

            new LocationPlanCommitter().Commit(plot, plan);

            Assert.DoesNotContain(removed, plot.Elements);
            Assert.DoesNotContain(plot.Relations, relation => ReferenceEquals(relation, inactiveContains));
            Assert.DoesNotContain(plot.Relations, relation => ReferenceEquals(relation, customInternal));
        }

        private static Element NewLocation()
        {
            return new Element(ElemType.Location);
        }

        private static void Add(Plot plot, params Element[] elements)
        {
            foreach (var element in elements) plot.Add(element);
        }

        private static void Relate(Plot plot, IElement source, IElement target, ParamKey<bool> key)
        {
            plot.Relations.Add(new Relation(source, target, key, true));
        }

        private sealed class PlotState
        {
            private readonly string _serialized;
            private readonly IElement[] _elements;
            private readonly Relation[] _relations;
            private readonly ElementState[] _elementStates;
            private readonly object?[] _relationValues;
            private readonly int _time;

            private PlotState(Plot plot)
            {
                _serialized = Serializer.SerializeToString(plot);
                _elements = plot.Elements.ToArray();
                _relations = plot.Relations.ToArray();
                _elementStates = _elements.Select(element => new ElementState(element)).ToArray();
                _relationValues = _relations.Select(relation => relation.Value).ToArray();
                _time = plot.Time;
            }

            public static PlotState Capture(Plot plot) => new PlotState(plot);

            public void AssertRestored(Plot plot)
            {
                Assert.Equal(_serialized, Serializer.SerializeToString(plot));
                Assert.Equal(_time, plot.Time);
                Assert.Equal(_elements.Length, plot.Elements.Count);
                Assert.Equal(_relations.Length, plot.Relations.Count);
                Assert.All(_elements.Zip(plot.Elements), pair => Assert.Same(pair.First, pair.Second));
                Assert.All(_relations.Zip(plot.Relations), pair => Assert.Same(pair.First, pair.Second));
                foreach (var element in _elementStates) element.AssertUnchanged();
                Assert.Equal(_relationValues, plot.Relations.Select(relation => relation.Value));
            }

            private sealed class ElementState
            {
                private readonly IElement _element;
                private readonly string _name;
                private readonly string _description;
                private readonly int _time;
                private readonly KeyValuePair<IParamKey, object?>[] _parameters;

                public ElementState(IElement element)
                {
                    _element = element;
                    _name = element.Name;
                    _description = element.Description;
                    _time = element.Time;
                    _parameters = element.Params.Enumerate().ToArray();
                }

                public void AssertUnchanged()
                {
                    Assert.Equal(_name, _element.Name);
                    Assert.Equal(_description, _element.Description);
                    Assert.Equal(_time, _element.Time);
                    Assert.Equal(_parameters, _element.Params.Enumerate());
                }
            }
        }
    }
}
