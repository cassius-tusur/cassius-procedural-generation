using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Builds and validates degree-bounded branching location topologies.
    /// </summary>
    public sealed class BranchingTopologyStrategy : ILocationTopologyStrategy
    {
        public string StrategyId => "Branching";
        public Type OptionsType => typeof(BranchingOptions);

        /// <summary>
        /// Validates options.
        /// </summary>
        /// <param name="options">The generation options.</param>
        public void ValidateOptions(ILocationTopologyOptions options)
        {
            if (!(options is BranchingOptions branching))
            {
                ThrowOptionsMismatch();
                return;
            }
            if (branching.MaxDegree < 2)
                ThrowIfAny(new[] { new LocationGenerationDiagnostic("options.invalid", "options.maxDegree",
                    "MaxDegree must be at least 2.") });
        }

        /// <summary>
        /// Builds a topology plan from the supplied generation context and options.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="options">The generation options.</param>
        /// <returns>The generated topology plan.</returns>
        public LocationStrategyPlan Build(LocationStrategyContext context, ILocationTopologyOptions options)
        {
            ValidateContext(context);
            ValidateOptions(options);
            var maxDegree = ((BranchingOptions)options).MaxDegree;
            var diagnostics = new List<LocationGenerationDiagnostic>();
            var existing = context.ExistingGraph.NodeIds;
            var newIds = context.NewNodeIds;
            ValidateIds(existing, newIds, diagnostics);
            ValidateBoundedTree(existing, context.ExistingGraph.Passages, maxDegree, diagnostics);
            EnsureNewNodes(newIds, diagnostics);
            if (diagnostics.Count > 0) ThrowIfAny(diagnostics);

            Guid? attachment = null;
            var existingDegrees = Degrees(existing, context.ExistingGraph.Passages);
            if (existing.Count > 0)
                attachment = SelectAttachment(context, existingDegrees, maxDegree);
            else if (context.AttachmentId.HasValue)
                ThrowIfAny(new[] { AttachmentDiagnostic(context.AttachmentId) });

            var passages = DegreeBoundedTreeBuilder.Build(newIds, maxDegree,
                attachment.HasValue ? newIds[0] : (Guid?)null, context.Random).ToList();
            if (attachment.HasValue)
                passages.Add(new LocationPassageProposal(attachment.Value, newIds[0]));
            return new LocationStrategyPlan(passages, attachment);
        }

        /// <summary>
        /// Validates plan.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="options">The generation options.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void ValidatePlan(LocationStrategyContext context, ILocationTopologyOptions options,
            LocationStrategyPlan plan)
        {
            ValidateContext(context);
            ValidateOptions(options);
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var maxDegree = ((BranchingOptions)options).MaxDegree;
            var diagnostics = new List<LocationGenerationDiagnostic>();
            var existing = context.ExistingGraph.NodeIds;
            var newIds = context.NewNodeIds;
            ValidateIds(existing, newIds, diagnostics);
            ValidateBoundedTree(existing, context.ExistingGraph.Passages, maxDegree, diagnostics);
            EnsureNewNodes(newIds, diagnostics);

            if (existing.Count == 0)
            {
                if (plan.AttachmentId.HasValue)
                    diagnostics.Add(AttachmentDiagnostic(plan.AttachmentId));
            }
            else
            {
                if (!plan.AttachmentId.HasValue)
                    diagnostics.Add(AttachmentDiagnostic(null));
                else if (!existing.Contains(plan.AttachmentId.Value))
                    diagnostics.Add(AttachmentDiagnostic(plan.AttachmentId));
                if (context.AttachmentId.HasValue && plan.AttachmentId != context.AttachmentId)
                    diagnostics.Add(AttachmentDiagnostic(context.AttachmentId));
            }

            var proposals = plan.Passages ?? Array.Empty<LocationPassageProposal>();
            ValidateProposalEndpoints(existing, newIds, proposals, diagnostics);
            ValidateAppendBridge(existing, newIds, plan.AttachmentId, proposals, diagnostics);
            var combinedNodes = existing.Concat(newIds).ToArray();
            if (combinedNodes.Length == combinedNodes.Distinct().Count())
                ValidateBoundedTree(combinedNodes, context.ExistingGraph.Passages.Concat(proposals).ToArray(),
                    maxDegree, diagnostics);
            var expectedPassages = existing.Count == 0 ? newIds.Count - 1 : newIds.Count;
            if (proposals.Count != expectedPassages)
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "A branching append requires a tree through every new node and one bridge."));
            if (diagnostics.Count > 0) ThrowIfAny(diagnostics);
        }

        /// <summary>
        /// Selects the node used to attach a new topology fragment.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="degrees">The degrees value.</param>
        /// <param name="maxDegree">The max degree value.</param>
        /// <returns>The resulting identifier.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be completed in the current state.</exception>
        private static Guid SelectAttachment(LocationStrategyContext context, IReadOnlyDictionary<Guid, int> degrees,
            int maxDegree)
        {
            var candidates = degrees.Where(pair => pair.Value < maxDegree)
                .Select(pair => pair.Key)
                .OrderBy(id => id)
                .ToArray();
            if (context.AttachmentId.HasValue)
            {
                if (!candidates.Contains(context.AttachmentId.Value))
                    ThrowIfAny(new[] { AttachmentDiagnostic(context.AttachmentId) });
                return context.AttachmentId.Value;
            }
            if (candidates.Length == 0) ThrowIfAny(new[] { AttachmentDiagnostic(null) });
            var index = candidates.Length == 1 ? 0 : context.Random.Next(candidates.Length);
            if (index < 0 || index >= candidates.Length)
                throw new InvalidOperationException("The location random source returned an out-of-range index.");
            return candidates[index];
        }

        /// <summary>
        /// Validates bounded tree.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="maxDegree">The max degree value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateBoundedTree(IReadOnlyList<Guid> nodes, IReadOnlyList<LocationPassageProposal> passages,
            int maxDegree, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (nodes.Count == 0)
            {
                if (passages.Count > 0)
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "Passages cannot exist without nodes."));
                return;
            }
            var nodeSet = new HashSet<Guid>(nodes);
            if (nodeSet.Count != nodes.Count)
            {
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.nodeIds", "Node IDs must be unique."));
                return;
            }
            var degrees = nodes.ToDictionary(id => id, _ => 0);
            var edges = new HashSet<(Guid First, Guid Second)>();
            foreach (var passage in passages)
            {
                if (passage.FirstId == passage.SecondId || !nodeSet.Contains(passage.FirstId) || !nodeSet.Contains(passage.SecondId))
                {
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "Passages must join distinct known nodes."));
                    continue;
                }
                if (!edges.Add(Canonical(passage.FirstId, passage.SecondId)))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "Passages must be unique."));
                degrees[passage.FirstId]++;
                degrees[passage.SecondId]++;
            }
            if (passages.Count != nodes.Count - 1 || !Connected(nodes, edges))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "The graph must be a connected tree."));
            if (degrees.Values.Any(degree => degree > maxDegree))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "The graph exceeds MaxDegree."));
        }

        /// <summary>
        /// Validates ids.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateIds(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (existing.Count != existing.Distinct().Count())
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "existingGraph.nodeIds", "Existing node IDs must be unique."));
            if (newIds.Count != newIds.Distinct().Count())
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "newNodeIds", "New node IDs must be unique."));
            var overlap = new HashSet<Guid>(existing);
            overlap.IntersectWith(newIds);
            if (overlap.Count > 0)
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "newNodeIds", "New node IDs must not overlap existing IDs.", overlap.ToArray()));
        }

        /// <summary>
        /// Validates proposal endpoints.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateProposalEndpoints(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            IReadOnlyList<LocationPassageProposal> passages, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var allowed = new HashSet<Guid>(existing);
            allowed.UnionWith(newIds);
            foreach (var passage in passages)
                if (!allowed.Contains(passage.FirstId) || !allowed.Contains(passage.SecondId))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                        "Plan passages must reference existing or newly created nodes.", new[] { passage.FirstId, passage.SecondId }));
        }

        /// <summary>
        /// Validates append bridge.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="attachment">The attachment value.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateAppendBridge(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            Guid? attachment, IReadOnlyList<LocationPassageProposal> passages,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (existing.Count == 0 || !attachment.HasValue) return;
            var oldIds = new HashSet<Guid>(existing);
            var newIdsSet = new HashSet<Guid>(newIds);
            var bridges = passages.Where(passage =>
                oldIds.Contains(passage.FirstId) && newIdsSet.Contains(passage.SecondId) ||
                oldIds.Contains(passage.SecondId) && newIdsSet.Contains(passage.FirstId)).ToArray();
            if (bridges.Length != 1)
            {
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "A branching append must contain exactly one old-to-new bridge."));
                return;
            }
            var bridge = bridges[0];
            var oldId = oldIds.Contains(bridge.FirstId) ? bridge.FirstId : bridge.SecondId;
            if (oldId != attachment.Value) diagnostics.Add(AttachmentDiagnostic(attachment));
        }

        /// <summary>
        /// Calculates the degree of each node in the supplied edge set.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="passages">The passages value.</param>
        /// <returns>The degree of each node in the supplied edge set.</returns>
        private static IReadOnlyDictionary<Guid, int> Degrees(IReadOnlyList<Guid> nodes,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            var degrees = nodes.ToDictionary(id => id, _ => 0);
            foreach (var passage in passages)
            {
                if (degrees.ContainsKey(passage.FirstId)) degrees[passage.FirstId]++;
                if (degrees.ContainsKey(passage.SecondId)) degrees[passage.SecondId]++;
            }
            return degrees;
        }

        /// <summary>
        /// Ensures that the strategy produced at least one new node.
        /// </summary>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void EnsureNewNodes(IReadOnlyList<Guid> newIds, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (newIds.Count == 0)
                diagnostics.Add(new LocationGenerationDiagnostic("nodeCount.invalid", "nodeCount", "At least one new node is required."));
        }
        /// <summary>
        /// Validates context.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        private static void ValidateContext(LocationStrategyContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
        }
        /// <summary>
        /// Throws a validation exception for an incompatible options type.
        /// </summary>
        private static void ThrowOptionsMismatch() => ThrowIfAny(new[]
        {
            new LocationGenerationDiagnostic("options.typeMismatch", "options", "does not match Branching.")
        });
        /// <summary>
        /// Determines whether the supplied graph is connected.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="edges">The edges value.</param>
        /// <returns><see langword="true"/> if the supplied graph is connected; otherwise, <see langword="false"/>.</returns>
        private static bool Connected(IReadOnlyList<Guid> nodes, HashSet<(Guid First, Guid Second)> edges)
        {
            var adjacency = nodes.ToDictionary(id => id, _ => new List<Guid>());
            foreach (var edge in edges) { adjacency[edge.First].Add(edge.Second); adjacency[edge.Second].Add(edge.First); }
            var seen = new HashSet<Guid>();
            var pending = new Queue<Guid>();
            pending.Enqueue(nodes[0]);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!seen.Add(current)) continue;
                foreach (var next in adjacency[current]) pending.Enqueue(next);
            }
            return seen.Count == nodes.Count;
        }
        /// <summary>
        /// Returns the identifiers in canonical order.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The identifiers in canonical order.</returns>
        private static (Guid First, Guid Second) Canonical(Guid first, Guid second) =>
            first.CompareTo(second) < 0 ? (first, second) : (second, first);
        /// <summary>
        /// Creates a diagnostic for an invalid topology attachment.
        /// </summary>
        /// <param name="id">The id value.</param>
        /// <returns>A diagnostic for the invalid topology attachment.</returns>
        private static LocationGenerationDiagnostic AttachmentDiagnostic(Guid? id) =>
            new LocationGenerationDiagnostic("attachment.unavailable", "attachmentId",
                "The attachment must have capacity and permit the complete branching result.",
                id.HasValue ? new[] { id.Value } : Array.Empty<Guid>());
        /// <summary>
        /// Throws a validation exception when diagnostics are present.
        /// </summary>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        private static void ThrowIfAny(IReadOnlyList<LocationGenerationDiagnostic> diagnostics)
        {
            if (diagnostics.Count > 0) throw new LocationGenerationValidationException(diagnostics);
        }
    }
}
