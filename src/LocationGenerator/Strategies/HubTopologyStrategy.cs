using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Builds and validates location topologies centered on a hub node.
    /// </summary>
    public sealed class HubTopologyStrategy : ILocationTopologyStrategy
    {
        public string StrategyId => "Hub";
        public Type OptionsType => typeof(HubOptions);

        /// <summary>
        /// Validates options.
        /// </summary>
        /// <param name="options">The generation options.</param>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        public void ValidateOptions(ILocationTopologyOptions options)
        {
            if (!(options is HubOptions))
                throw new LocationGenerationValidationException(new[]
                {
                    new LocationGenerationDiagnostic("options.typeMismatch", "options", "does not match Hub.")
                });
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
            var diagnostics = new List<LocationGenerationDiagnostic>();
            var existing = context.ExistingGraph.NodeIds;
            var newIds = context.NewNodeIds;
            ValidateIds(existing, newIds, diagnostics);
            ValidateStar(existing, context.ExistingGraph.Passages, diagnostics);
            EnsureNewNodes(newIds, diagnostics);
            if (diagnostics.Count > 0) ThrowIfAny(diagnostics);

            Guid? center = null;
            if (existing.Count > 0)
            {
                var candidates = StarCenters(existing, context.ExistingGraph.Passages);
                center = SelectCenter(context, candidates);
            }
            else if (context.AttachmentId.HasValue)
            {
                ThrowIfAny(new[] { AttachmentDiagnostic(context.AttachmentId) });
            }

            var passages = new List<LocationPassageProposal>();
            if (center.HasValue)
            {
                foreach (var newId in newIds)
                    passages.Add(new LocationPassageProposal(center.Value, newId));
            }
            else
            {
                var newCenter = newIds[0];
                for (var index = 1; index < newIds.Count; index++)
                    passages.Add(new LocationPassageProposal(newCenter, newIds[index]));
            }
            return new LocationStrategyPlan(passages, center);
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

            var diagnostics = new List<LocationGenerationDiagnostic>();
            var existing = context.ExistingGraph.NodeIds;
            var newIds = context.NewNodeIds;
            ValidateIds(existing, newIds, diagnostics);
            ValidateStar(existing, context.ExistingGraph.Passages, diagnostics);
            EnsureNewNodes(newIds, diagnostics);
            var centers = existing.Count == 0
                ? Array.Empty<Guid>()
                : StarCenters(existing, context.ExistingGraph.Passages);

            if (existing.Count == 0)
            {
                if (plan.AttachmentId.HasValue)
                    diagnostics.Add(AttachmentDiagnostic(plan.AttachmentId));
            }
            else
            {
                if (!plan.AttachmentId.HasValue || !centers.Contains(plan.AttachmentId.Value))
                    diagnostics.Add(AttachmentDiagnostic(plan.AttachmentId));
                if (context.AttachmentId.HasValue && plan.AttachmentId != context.AttachmentId)
                    diagnostics.Add(AttachmentDiagnostic(context.AttachmentId));
            }

            var proposalPassages = plan.Passages ?? Array.Empty<LocationPassageProposal>();
            var combined = context.ExistingGraph.Passages.Concat(proposalPassages).ToArray();
            var combinedNodes = existing.Concat(newIds).ToArray();
            if (combinedNodes.Length == combinedNodes.Distinct().Count())
                ValidateStar(combinedNodes, combined, diagnostics);
            ValidateProposalEndpoints(existing, newIds, proposalPassages, diagnostics);
            ValidateAppendSpokes(existing, newIds, plan.AttachmentId, proposalPassages, diagnostics);
            if (proposalPassages.Count != (existing.Count == 0 ? newIds.Count - 1 : newIds.Count))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "A hub requires one spoke for every new node when appending."));
            if (diagnostics.Count > 0) ThrowIfAny(diagnostics);
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
        /// Ensures that the strategy produced at least one new node.
        /// </summary>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void EnsureNewNodes(IReadOnlyList<Guid> newIds,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (newIds.Count == 0)
                diagnostics.Add(new LocationGenerationDiagnostic("nodeCount.invalid", "nodeCount", "At least one new node is required."));
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
        /// Validates star.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateStar(IReadOnlyList<Guid> nodes,
            IReadOnlyList<LocationPassageProposal> passages,
            ICollection<LocationGenerationDiagnostic> diagnostics)
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
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "Passages must join distinct known nodes.",
                        new[] { passage.FirstId, passage.SecondId }));
                    continue;
                }
                var edge = Canonical(passage.FirstId, passage.SecondId);
                if (!edges.Add(edge))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "Passages must be unique.",
                        new[] { passage.FirstId, passage.SecondId }));
                degrees[passage.FirstId]++;
                degrees[passage.SecondId]++;
            }
            if (passages.Count != nodes.Count - 1)
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "A star must have V-1 edges."));
            if (!Connected(nodes, edges))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "The star must be connected."));
            if (nodes.Count == 1)
            {
                if (degrees[nodes[0]] != 0)
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "A one-node star has no edges."));
                return;
            }
            if (nodes.Count == 2)
            {
                if (degrees.Values.Any(degree => degree != 1))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "A two-node star must have one edge."));
                return;
            }
            var maxDegree = degrees.Values.Max();
            if (maxDegree != nodes.Count - 1 || degrees.Values.Count(degree => degree == maxDegree) != 1 ||
                degrees.Values.Any(degree => degree != 1 && degree != maxDegree))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "The graph must be a strict star."));
        }

        /// <summary>
        /// Validates proposal endpoints.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateProposalEndpoints(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            IReadOnlyList<LocationPassageProposal> passages,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var allowed = new HashSet<Guid>(existing);
            allowed.UnionWith(newIds);
            foreach (var passage in passages)
                if (!allowed.Contains(passage.FirstId) || !allowed.Contains(passage.SecondId))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                        "Plan passages must reference existing or newly created nodes.",
                        new[] { passage.FirstId, passage.SecondId }));
        }

        /// <summary>
        /// Validates append spokes.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="center">The center value.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateAppendSpokes(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            Guid? center, IReadOnlyList<LocationPassageProposal> passages,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (existing.Count == 0 || !center.HasValue) return;
            var existingSet = new HashSet<Guid>(existing);
            var newSet = new HashSet<Guid>(newIds);
            var seenNew = new HashSet<Guid>();
            foreach (var passage in passages)
            {
                var firstIsOld = existingSet.Contains(passage.FirstId);
                var secondIsOld = existingSet.Contains(passage.SecondId);
                var firstIsNew = newSet.Contains(passage.FirstId);
                var secondIsNew = newSet.Contains(passage.SecondId);
                if (!firstIsOld && !secondIsOld || !firstIsNew && !secondIsNew || firstIsOld == secondIsOld)
                {
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                        "A hub append must contain only old-to-new spokes."));
                    continue;
                }
                var oldId = firstIsOld ? passage.FirstId : passage.SecondId;
                var newId = firstIsNew ? passage.FirstId : passage.SecondId;
                if (oldId != center.Value)
                    diagnostics.Add(AttachmentDiagnostic(center));
                if (!seenNew.Add(newId))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                        "A hub append must connect each new node exactly once."));
            }
            if (seenNew.Count != newIds.Count)
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "A hub append must contain one spoke for every new node."));
        }

        /// <summary>
        /// Finds nodes that can serve as the center of the proposed star graph.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="passages">The passages value.</param>
        /// <returns>The nodes that can serve as the center of the proposed star graph.</returns>
        private static Guid[] StarCenters(IReadOnlyList<Guid> nodes,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            if (nodes.Count == 1) return new[] { nodes[0] };
            var degrees = nodes.ToDictionary(id => id, _ => 0);
            foreach (var passage in passages)
            {
                if (degrees.ContainsKey(passage.FirstId)) degrees[passage.FirstId]++;
                if (degrees.ContainsKey(passage.SecondId)) degrees[passage.SecondId]++;
            }
            return nodes.Where(id => degrees[id] == nodes.Count - 1).ToArray();
        }

        /// <summary>
        /// Selects the center node for the hub topology.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="candidates">The candidates value.</param>
        /// <returns>The resulting identifier.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be completed in the current state.</exception>
        private static Guid SelectCenter(LocationStrategyContext context, IReadOnlyList<Guid> candidates)
        {
            if (context.AttachmentId.HasValue)
            {
                if (!candidates.Contains(context.AttachmentId.Value))
                    ThrowIfAny(new[] { AttachmentDiagnostic(context.AttachmentId) });
                return context.AttachmentId.Value;
            }
            var sorted = candidates.OrderBy(id => id).ToArray();
            var index = context.Random.Next(sorted.Length);
            if (index < 0 || index >= sorted.Length)
                throw new InvalidOperationException("The location random source returned an out-of-range index.");
            return sorted[index];
        }

        /// <summary>
        /// Determines whether the supplied graph is connected.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="edges">The edges value.</param>
        /// <returns><see langword="true"/> if the supplied graph is connected; otherwise, <see langword="false"/>.</returns>
        private static bool Connected(IReadOnlyList<Guid> nodes, HashSet<(Guid First, Guid Second)> edges)
        {
            if (nodes.Count == 0) return true;
            var adjacency = nodes.ToDictionary(id => id, _ => new List<Guid>());
            foreach (var edge in edges)
            {
                adjacency[edge.First].Add(edge.Second);
                adjacency[edge.Second].Add(edge.First);
            }
            var visited = new HashSet<Guid>();
            var pending = new Queue<Guid>();
            pending.Enqueue(nodes[0]);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                foreach (var neighbor in adjacency[current]) pending.Enqueue(neighbor);
            }
            return visited.Count == nodes.Count;
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
                "The attachment must identify the existing hub center.",
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
