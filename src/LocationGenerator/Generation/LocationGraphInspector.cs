using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Services.Binds;
using LocationGenerator.Contracts;
using LocationGenerator.Params;
using LocationGenerator.Strategies;

namespace LocationGenerator.Generation
{
    /// <summary>
    /// Inspects and validates the location graph before topology generation.
    /// </summary>
    internal sealed class LocationGraphInspector
    {
        /// <summary>
        /// Validates root.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="root">The root location for the operation.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void ValidateRoot(Plot plot, IElement root)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (root == null) throw new ArgumentNullException(nameof(root));
            var diagnostics = new List<LocationGenerationDiagnostic>();
            var matching = plot.Elements.Where(element => element.Id == root.Id).ToList();
            if (matching.Count != 1)
            {
                diagnostics.Add(Diagnostic("root.invalid", "root", "Root must identify exactly one Plot element.", root.Id));
            }
            else if (matching[0].Type != ElemType.Location)
            {
                diagnostics.Add(Diagnostic("root.type", "root.type", "Root must be a Location.", matching[0].Id));
            }

            ThrowIfAny(diagnostics);
        }

        /// <summary>
        /// Inspects the supplied value.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="root">The root location for the operation.</param>
        /// <param name="mode">The application or generation mode.</param>
        /// <returns>The generation scope derived from the current graph.</returns>
        public LocationGenerationScope Inspect(Plot plot, IElement root, LocationGenerationMode mode)
        {
            ValidateRoot(plot, root);
            var index = BuildIndex(plot);
            var actualRoot = index[root.Id].Single();
            return mode == LocationGenerationMode.Append
                ? InspectAppend(plot, actualRoot, index)
                : InspectOverwrite(plot, actualRoot, index);
        }

        /// <summary>
        /// Inspects the existing graph and prepares an append-mode generation scope.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="root">The root location for the operation.</param>
        /// <param name="index">The index value.</param>
        /// <returns>The append-mode generation scope.</returns>
        private static LocationGenerationScope InspectAppend(Plot plot, IElement root,
            IReadOnlyDictionary<Guid, List<IElement>> index)
        {
            var diagnostics = new List<LocationGenerationDiagnostic>();
            var children = new Dictionary<Guid, IElement>();
            var directContains = ActiveContainsFrom(plot, root.Id).ToList();

            foreach (var relation in directContains)
            {
                var target = Resolve(index, relation.Target.Id, diagnostics, "graph.contains.target");
                if (target == null) continue;
                if (target.Type != ElemType.Location)
                {
                    diagnostics.Add(Diagnostic("graph.contains.nonLocation", "graph.contains", "Contains children must be Locations.", target.Id));
                    continue;
                }
                if (children.ContainsKey(target.Id))
                    diagnostics.Add(Diagnostic("graph.contains.duplicate", "graph.contains", "Contains edges must be unique.", target.Id));
                else
                    children.Add(target.Id, target);
            }

            var localIds = new HashSet<Guid>(children.Keys) { root.Id };
            EnsureUnambiguous(index, localIds, diagnostics);
            ValidateImmediateContains(plot, root.Id, localIds, children.Keys, diagnostics);
            var passages = ValidateLocalPassages(plot, children.Keys, diagnostics);
            ThrowIfAny(diagnostics);

            var locations = new Dictionary<Guid, IElement>(children);
            return new LocationGenerationScope(root,
                new LocationGraphSnapshot(children.Keys.OrderBy(id => id).ToArray(), passages), locations,
                Array.Empty<IElement>(), Array.Empty<Relation>());
        }

        /// <summary>
        /// Inspects the existing graph and prepares an overwrite-mode generation scope.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="root">The root location for the operation.</param>
        /// <param name="index">The index value.</param>
        /// <returns>The overwrite-mode generation scope.</returns>
        private static LocationGenerationScope InspectOverwrite(Plot plot, IElement root,
            IReadOnlyDictionary<Guid, List<IElement>> index)
        {
            var diagnostics = new List<LocationGenerationDiagnostic>();
            var descendants = new Dictionary<Guid, IElement>();
            var parentCounts = new Dictionary<Guid, int>();
            var states = new Dictionary<Guid, int> { [root.Id] = 1 };
            var stack = new Stack<TraversalFrame>();
            stack.Push(new TraversalFrame(root.Id, ActiveContainsFrom(plot, root.Id).ToList()));

            while (stack.Count > 0)
            {
                var frame = stack.Peek();
                if (frame.Next == frame.Relations.Count)
                {
                    states[frame.Id] = 2;
                    stack.Pop();
                    continue;
                }

                var relation = frame.Relations[frame.Next++];
                var target = Resolve(index, relation.Target.Id, diagnostics, "graph.contains.target");
                if (target == null) continue;
                if (target.Type != ElemType.Location)
                {
                    diagnostics.Add(Diagnostic("graph.contains.nonLocation", "graph.contains", "Contains children must be Locations.", target.Id));
                    continue;
                }

                parentCounts[target.Id] = parentCounts.TryGetValue(target.Id, out var count) ? count + 1 : 1;
                if (target.Id == root.Id || states.TryGetValue(target.Id, out var state) && state == 1)
                {
                    diagnostics.Add(Diagnostic("graph.contains.cycle", "graph.contains", "Contains edges must not form a cycle.", frame.Id, target.Id));
                    continue;
                }
                if (states.TryGetValue(target.Id, out state)) continue;

                descendants[target.Id] = target;
                states[target.Id] = 1;
                stack.Push(new TraversalFrame(target.Id, ActiveContainsFrom(plot, target.Id).ToList()));
            }

            var affectedIds = new HashSet<Guid>(descendants.Keys) { root.Id };
            EnsureUnambiguous(index, affectedIds, diagnostics);
            foreach (var descendantId in descendants.Keys)
            {
                if (!parentCounts.TryGetValue(descendantId, out var count) || count != 1)
                    diagnostics.Add(Diagnostic("graph.contains.multipleParents", "graph.contains", "Each contained location must have one active Contains parent.", descendantId));

                var allParents = ActiveContainsTo(plot, descendantId).ToList();
                if (allParents.Count != 1)
                    diagnostics.Add(Diagnostic("graph.contains.multipleParents", "graph.contains", "Each contained location must have one active Contains parent.", descendantId));
            }

            var removedRelations = new List<Relation>();
            var crossing = new List<LocationRelationReference>();
            foreach (var relation in plot.Relations)
            {
                var sourceInside = descendants.ContainsKey(relation.Source.Id);
                var targetInside = descendants.ContainsKey(relation.Target.Id);
                if (!sourceInside && !targetInside) continue;
                if (sourceInside && targetInside)
                {
                    removedRelations.Add(relation);
                    continue;
                }

                if (IsAllowedRootContains(relation, root.Id, descendants))
                {
                    removedRelations.Add(relation);
                    continue;
                }
                crossing.Add(Reference(relation));
            }

            if (crossing.Count > 0)
            {
                diagnostics.Add(new LocationGenerationDiagnostic("overwrite.externalReference", "overwrite.boundary",
                    "Relations crossing the overwrite boundary are not allowed.",
                    crossing.SelectMany(reference => new[] { reference.SourceId, reference.TargetId }).Distinct().ToArray(), crossing));
            }
            ThrowIfAny(diagnostics);

            return new LocationGenerationScope(root,
                new LocationGraphSnapshot(Array.Empty<Guid>(), Array.Empty<LocationPassageProposal>()),
                new Dictionary<Guid, IElement>(), descendants.Values.OrderBy(element => element.Id).ToArray(), removedRelations);
        }

        /// <summary>
        /// Validates immediate contains.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="rootId">The identifier of the root.</param>
        /// <param name="localIds">The identifiers of the local values.</param>
        /// <param name="childIds">The identifiers of the child values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateImmediateContains(Plot plot, Guid rootId, ISet<Guid> localIds,
            IEnumerable<Guid> childIds, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var childSet = new HashSet<Guid>(childIds);
            foreach (var childId in childSet)
            {
                var parents = ActiveContainsTo(plot, childId).ToList();
                if (parents.Count != 1 || parents[0].Source.Id != rootId)
                    diagnostics.Add(Diagnostic("graph.contains.multipleParents", "graph.contains", "Each direct child must have only Root as its active Contains parent.", childId));
            }

            foreach (var relation in plot.Relations.Where(IsActiveContains))
            {
                if (!localIds.Contains(relation.Source.Id) || !localIds.Contains(relation.Target.Id)) continue;
                if (relation.Source.Id == relation.Target.Id || relation.Target.Id == rootId || relation.Source.Id != rootId)
                    diagnostics.Add(Diagnostic("graph.contains.cycle", "graph.contains", "Contains edges must not form a local cycle.", relation.Source.Id, relation.Target.Id));
            }
        }

        /// <summary>
        /// Validates local passages.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="childIds">The identifiers of the child values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        /// <returns>The resulting collection.</returns>
        private static IReadOnlyList<LocationPassageProposal> ValidateLocalPassages(Plot plot,
            IEnumerable<Guid> childIds, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var local = new HashSet<Guid>(childIds);
            var directed = new HashSet<(Guid Source, Guid Target)>();
            var undirected = new Dictionary<(Guid First, Guid Second), int>();
            foreach (var relation in plot.Relations.Where(IsActivePassage))
            {
                if (!local.Contains(relation.Source.Id) || !local.Contains(relation.Target.Id)) continue;
                if (relation.Source.Id == relation.Target.Id)
                {
                    diagnostics.Add(Diagnostic("graph.passage.loop", "graph.passage", "Passage edges cannot be loops.", relation.Source.Id));
                    continue;
                }
                if (!directed.Add((relation.Source.Id, relation.Target.Id)))
                    diagnostics.Add(Diagnostic("graph.passage.duplicate", "graph.passage", "Passage edges must be unique.", relation.Source.Id, relation.Target.Id));

                var pair = relation.Source.Id.CompareTo(relation.Target.Id) < 0
                    ? (relation.Source.Id, relation.Target.Id) : (relation.Target.Id, relation.Source.Id);
                undirected[pair] = undirected.TryGetValue(pair, out var count) ? count + 1 : 1;
            }

            foreach (var pair in undirected.Where(pair => pair.Value != 2 ||
                !directed.Contains((pair.Key.First, pair.Key.Second)) || !directed.Contains((pair.Key.Second, pair.Key.First))))
            {
                diagnostics.Add(Diagnostic("graph.passage.unpaired", "graph.passage", "Passage edges must be reciprocal.", pair.Key.First, pair.Key.Second));
            }
            return undirected.Keys.OrderBy(pair => pair.First).ThenBy(pair => pair.Second)
                .Select(pair => new LocationPassageProposal(pair.First, pair.Second)).ToArray();
        }

        /// <summary>
        /// Builds index.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <returns>The index.</returns>
        private static Dictionary<Guid, List<IElement>> BuildIndex(Plot plot) => plot.Elements
            .GroupBy(element => element.Id).ToDictionary(group => group.Key, group => group.ToList());

        /// <summary>
        /// Resolves an element identifier through the graph index.
        /// </summary>
        /// <param name="index">The index value.</param>
        /// <param name="id">The id value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        /// <param name="path">The path value.</param>
        /// <returns>The uniquely resolved element, or <see langword="null"/> when resolution fails.</returns>
        private static IElement? Resolve(IReadOnlyDictionary<Guid, List<IElement>> index, Guid id,
            ICollection<LocationGenerationDiagnostic> diagnostics, string path)
        {
            if (!index.TryGetValue(id, out var elements) || elements.Count != 1)
            {
                diagnostics.Add(Diagnostic("graph.element.invalid", path, "Relation endpoint must identify exactly one Plot element.", id));
                return null;
            }
            return elements[0];
        }

        /// <summary>
        /// Ensures that an identifier resolves to at most one element.
        /// </summary>
        /// <param name="index">The index value.</param>
        /// <param name="ids">The ids value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void EnsureUnambiguous(IReadOnlyDictionary<Guid, List<IElement>> index,
            IEnumerable<Guid> ids, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            foreach (var id in ids.Where(id => index.TryGetValue(id, out var elements) && elements.Count != 1))
                diagnostics.Add(Diagnostic("graph.element.ambiguous", "graph.elements", "Affected elements must have unique Ids.", id));
        }

        /// <summary>
        /// Returns active containment relations that originate from the specified element.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="sourceId">The identifier of the source.</param>
        /// <returns>The resulting collection.</returns>
        private static IEnumerable<Relation> ActiveContainsFrom(Plot plot, Guid sourceId) =>
            plot.Relations.Where(relation => IsActiveContains(relation) && relation.Source.Id == sourceId);

        /// <summary>
        /// Returns active containment relations that target the specified element.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="targetId">The identifier of the target.</param>
        /// <returns>The resulting collection.</returns>
        private static IEnumerable<Relation> ActiveContainsTo(Plot plot, Guid targetId) =>
            plot.Relations.Where(relation => IsActiveContains(relation) && relation.Target.Id == targetId);

        /// <summary>
        /// Determines whether the relation is an active containment relation.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <returns><see langword="true"/> if the relation is an active containment relation; otherwise, <see langword="false"/>.</returns>
        private static bool IsActiveContains(Relation relation) => IsActive(relation, BaseRelationKeys.Contains);
        /// <summary>
        /// Determines whether the relation is an active passage relation.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <returns><see langword="true"/> if the relation is an active passage relation; otherwise, <see langword="false"/>.</returns>
        private static bool IsActivePassage(Relation relation) => IsActive(relation, BaseRelationKeys.Passage);
        /// <summary>
        /// Determines whether the relation is active for the specified key.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <param name="key">The parameter key to use.</param>
        /// <returns><see langword="true"/> if the relation is active for the specified key; otherwise, <see langword="false"/>.</returns>
        private static bool IsActive(Relation relation, IParamKey key) =>
            relation.Param.Equals(key) && relation.Value is bool value && value;

        /// <summary>
        /// Determines whether the root containment relation may remain in the generation scope.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <param name="rootId">The identifier of the root.</param>
        /// <param name="descendants">The descendants value.</param>
        /// <returns><see langword="true"/> if the root containment relation may remain in the generation scope; otherwise, <see langword="false"/>.</returns>
        private static bool IsAllowedRootContains(Relation relation, Guid rootId,
            IReadOnlyDictionary<Guid, IElement> descendants) =>
            IsActiveContains(relation) && relation.Source.Id == rootId && descendants.ContainsKey(relation.Target.Id) ||
            IsActive(relation, BaseRelationKeys.ContainedBy) && relation.Target.Id == rootId &&
            descendants.ContainsKey(relation.Source.Id);

        /// <summary>
        /// Creates a stable reference to the supplied relation.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <returns>A stable reference to the supplied relation.</returns>
        private static LocationRelationReference Reference(Relation relation) =>
            new LocationRelationReference(relation.Source.Id, relation.Target.Id, relation.Param.Namespace, relation.Param.Name);

        /// <summary>
        /// Creates a location-generation diagnostic with the supplied context.
        /// </summary>
        /// <param name="code">The code value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="message">The message value.</param>
        /// <param name="ids">The ids value.</param>
        /// <returns>The generated diagnostic.</returns>
        private static LocationGenerationDiagnostic Diagnostic(string code, string path, string message, params Guid[] ids) =>
            new LocationGenerationDiagnostic(code, path, message, ids);

        /// <summary>
        /// Throws a validation exception when diagnostics are present.
        /// </summary>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        private static void ThrowIfAny(IReadOnlyList<LocationGenerationDiagnostic> diagnostics)
        {
            if (diagnostics.Count > 0) throw new LocationGenerationValidationException(diagnostics);
        }

        /// <summary>
        /// Stores the mutable state of one iterative graph-traversal frame.
        /// </summary>
        private sealed class TraversalFrame
        {
            /// <summary>
            /// Initializes a new instance of <see cref="TraversalFrame"/>.
            /// </summary>
            /// <param name="id">The id value.</param>
            /// <param name="relations">The relations to process.</param>
            public TraversalFrame(Guid id, List<Relation> relations)
            {
                Id = id;
                Relations = relations;
            }

            public Guid Id { get; }
            public List<Relation> Relations { get; }
            public int Next { get; set; }
        }
    }
}
