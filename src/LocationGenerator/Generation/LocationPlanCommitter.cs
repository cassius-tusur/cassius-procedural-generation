using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses.Interface;
using BaseClasses.Model;

namespace LocationGenerator.Generation
{
    /// <summary>
    /// Applies a validated location mutation plan atomically to a plot.
    /// </summary>
    internal sealed class LocationPlanCommitter
    {
        /// <summary>
        /// Commits the supplied value.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void Commit(Plot plot, LocationMutationPlan plan)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            Validate(plot, plan);
            var snapshot = PlotSnapshot.Capture(plot);
            try
            {
                foreach (var relation in plan.RemovedRelations)
                {
                    if (!plot.Relations.Any(existing => ReferenceEquals(existing, relation))) continue;
                    plot.Unbind(relation.Source, relation.Target, relation.Param);
                    plot.Relations.Remove(relation);
                }
                foreach (var location in plan.RemovedLocations) plot.Remove(location);
                foreach (var location in plan.NewLocations) plot.Add(location);
                foreach (var relation in plan.Relations)
                    plot.Bind(relation.Source, relation.Target, relation.Param, relation.Value);
            }
            catch
            {
                snapshot.Restore(plot);
                throw;
            }
        }

        /// <summary>
        /// Validates that the mutation plan can be applied to the supplied plot.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <exception cref="ArgumentException">Thrown when an argument is invalid.</exception>
        private static void Validate(Plot plot, LocationMutationPlan plan)
        {
            var existing = plot.Elements.ToList();
            var existingRelations = plot.Relations.ToList();
            var removedRelations = new List<Relation>();
            foreach (var relation in plan.RemovedRelations)
            {
                if (removedRelations.Any(existing => ReferenceEquals(existing, relation)))
                    throw new ArgumentException("A relation may be removed only once.", nameof(plan));
                if (!existingRelations.Any(existing => ReferenceEquals(existing, relation)))
                    throw new ArgumentException("Every removed relation must belong to the plot.", nameof(plan));
                removedRelations.Add(relation);
            }

            var removed = new HashSet<Guid>();
            foreach (var location in plan.RemovedLocations)
            {
                if (!removed.Add(location.Id))
                    throw new ArgumentException("A location may be removed only once.", nameof(plan));
                if (!Contains(existing, location))
                    throw new ArgumentException("Every removed location must belong to the plot.", nameof(plan));
            }

            var known = existing.Where(element => !removed.Contains(element.Id)).ToList();
            var additions = new HashSet<Guid>();
            foreach (var location in plan.NewLocations)
            {
                if (!additions.Add(location.Id))
                    throw new ArgumentException("A location may be added only once.", nameof(plan));
                if (Contains(existing, location))
                    throw new ArgumentException("A new location must not already belong to the plot.", nameof(plan));
                known.Add(location);
            }

            foreach (var relation in plan.Relations)
            {
                if (!Contains(known, relation.Source) || !Contains(known, relation.Target))
                    throw new ArgumentException("Every relation endpoint must remain in the plot.", nameof(plan));
                if (!relation.Param.IsTypeMatch(relation.Value))
                    throw new ArgumentException("A relation value does not match its parameter key.", nameof(plan));
            }
        }

        /// <summary>
        /// Determines whether the collection contains the specified value.
        /// </summary>
        /// <param name="elements">The elements value.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns><see langword="true"/> if the collection contains the specified value; otherwise, <see langword="false"/>.</returns>
        private static bool Contains(IEnumerable<IElement> elements, IElement expected)
        {
            return elements.Any(element => element.Id == expected.Id);
        }

        /// <summary>
        /// Captures mutable plot state so a failed commit can be rolled back.
        /// </summary>
        private sealed class PlotSnapshot
        {
            private readonly IReadOnlyList<ElementSnapshot> _elements;
            private readonly IReadOnlyList<RelationSnapshot> _relations;
            private readonly int _time;

            /// <summary>
            /// Initializes a new instance of <see cref="PlotSnapshot"/>.
            /// </summary>
            /// <param name="elements">The elements value.</param>
            /// <param name="relations">The relations to process.</param>
            /// <param name="time">The time value.</param>
            private PlotSnapshot(
                IReadOnlyList<ElementSnapshot> elements,
                IReadOnlyList<RelationSnapshot> relations,
                int time)
            {
                _elements = elements;
                _relations = relations;
                _time = time;
            }

            /// <summary>
            /// Captures the current plot state so it can be restored after a failed commit.
            /// </summary>
            /// <param name="plot">The plot that contains the affected elements.</param>
            /// <returns>A snapshot of the plot's mutable state.</returns>
            public static PlotSnapshot Capture(Plot plot)
            {
                return new PlotSnapshot(
                    plot.Elements.Select(element => new ElementSnapshot(element)).ToList(),
                    plot.Relations.Select(relation => new RelationSnapshot(relation)).ToList(),
                    plot.Time);
            }

            /// <summary>
            /// Restores the supplied value.
            /// </summary>
            /// <param name="plot">The plot that contains the affected elements.</param>
            public void Restore(Plot plot)
            {
                foreach (var element in _elements) element.Restore();
                foreach (var relation in _relations) relation.Restore();
                plot.Relations.Clear();
                plot.Elements.Clear();
                foreach (var element in _elements) plot.Elements.Add(element.Element);
                foreach (var relation in _relations) plot.Relations.Add(relation.Relation);
                plot.Time = _time;
            }
        }

        /// <summary>
        /// Captures an element's mutable state for rollback.
        /// </summary>
        private sealed class ElementSnapshot
        {
            private readonly IElement _element;
            private readonly IReadOnlyList<KeyValuePair<BaseClasses.Interface.IParamKey, object?>> _entries;
            private readonly string _name;
            private readonly string _description;
            private readonly int _time;

            /// <summary>
            /// Initializes a new instance of <see cref="ElementSnapshot"/>.
            /// </summary>
            /// <param name="element">The element to process.</param>
            public ElementSnapshot(IElement element)
            {
                _element = element;
                _entries = element.Params.Enumerate().ToList();
                _name = element.Name;
                _description = element.Description;
                _time = element.Time;
            }

            public IElement Element => _element;

            /// <summary>
            /// Restores the supplied value.
            /// </summary>
            public void Restore()
            {
                _element.Name = _name;
                _element.Description = _description;
                _element.Time = _time;
                _element.Params.Clear();
                foreach (var entry in _entries) _element.Params.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Captures a relation's mutable state for rollback.
        /// </summary>
        private sealed class RelationSnapshot
        {
            private readonly Relation _relation;
            private readonly object? _value;

            /// <summary>
            /// Initializes a new instance of <see cref="RelationSnapshot"/>.
            /// </summary>
            /// <param name="relation">The relation to process.</param>
            public RelationSnapshot(Relation relation)
            {
                _relation = relation;
                _value = relation.Value;
            }

            public Relation Relation => _relation;

            /// <summary>
            /// Restores the supplied value.
            /// </summary>
            public void Restore()
            {
                _relation.Value = _value;
            }
        }
    }
}
