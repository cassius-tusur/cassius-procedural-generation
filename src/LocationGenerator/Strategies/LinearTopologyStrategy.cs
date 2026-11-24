using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Builds and validates path-shaped location topologies.
    /// </summary>
    public sealed class LinearTopologyStrategy : ILocationTopologyStrategy
    {
        public string StrategyId => "Linear";
        public Type OptionsType => typeof(LinearOptions);

        /// <summary>
        /// Validates options.
        /// </summary>
        /// <param name="options">The generation options.</param>
        public void ValidateOptions(ILocationTopologyOptions options)
        {
            if (!(options is LinearOptions)) ThrowOptionsMismatch();
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
            ValidateSimplePath(existing, context.ExistingGraph.Passages, diagnostics);
            EnsureNewNodes(newIds, diagnostics);
            if (diagnostics.Count > 0) ThrowIfAny(diagnostics);

            Guid? attachment = null;
            if (existing.Count > 0)
            {
                var endpoints = PathEndpoints(existing, context.ExistingGraph.Passages);
                attachment = SelectAttachment(context, endpoints);
            }
            else if (context.AttachmentId.HasValue)
            {
                ThrowIfAny(new[] { AttachmentDiagnostic(context.AttachmentId.Value) });
            }

            var passages = NewPath(newIds).ToList();
            if (attachment.HasValue) passages.Add(new LocationPassageProposal(attachment.Value, newIds[0]));
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

            var diagnostics = new List<LocationGenerationDiagnostic>();
            var existing = context.ExistingGraph.NodeIds;
            var newIds = context.NewNodeIds;
            ValidateIds(existing, newIds, diagnostics);
            ValidateSimplePath(existing, context.ExistingGraph.Passages, diagnostics);
            EnsureNewNodes(newIds, diagnostics);

            var endpoints = existing.Count == 0
                ? Array.Empty<Guid>()
                : PathEndpoints(existing, context.ExistingGraph.Passages);
            if (existing.Count == 0)
            {
                if (plan.AttachmentId.HasValue)
                    diagnostics.Add(AttachmentDiagnostic(plan.AttachmentId.Value));
            }
            else
            {
                if (!plan.AttachmentId.HasValue || !endpoints.Contains(plan.AttachmentId.Value))
                    diagnostics.Add(AttachmentDiagnostic(plan.AttachmentId));
                if (context.AttachmentId.HasValue && plan.AttachmentId != context.AttachmentId)
                    diagnostics.Add(AttachmentDiagnostic(context.AttachmentId));
            }

            var proposalPassages = plan.Passages ?? Array.Empty<LocationPassageProposal>();
            var combined = context.ExistingGraph.Passages.Concat(proposalPassages).ToArray();
            var combinedNodes = existing.Concat(newIds).ToArray();
            if (combinedNodes.Length == combinedNodes.Distinct().Count())
                ValidateSimplePath(combinedNodes, combined, diagnostics);
            ValidateProposalEndpoints(existing, newIds, proposalPassages, diagnostics);
            ValidateAppendBridge(existing, newIds, plan.AttachmentId, proposalPassages, diagnostics);
            if (proposalPassages.Count != (existing.Count == 0 ? newIds.Count - 1 : newIds.Count))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "A linear append requires one bridge and a path through every new node."));
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
        /// Throws a validation exception for an incompatible options type.
        /// </summary>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        private static void ThrowOptionsMismatch()
        {
            throw new LocationGenerationValidationException(new[]
            {
                new LocationGenerationDiagnostic("options.typeMismatch", "options", "does not match Linear.")
            });
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
        /// Validates simple path.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateSimplePath(IReadOnlyList<Guid> nodes,
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
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "A path must have V-1 edges."));
            if (degrees.Values.Any(degree => degree > 2) ||
                (nodes.Count == 1 && degrees[nodes[0]] != 0) ||
                (nodes.Count > 1 && degrees.Values.Count(degree => degree == 1) != 2) ||
                (nodes.Count > 2 && degrees.Values.Any(degree => degree == 0)))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "The graph must be a simple path."));
            if (!Connected(nodes, edges))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "graph.passages", "The path must be connected."));
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
            var existingSet = new HashSet<Guid>(existing);
            var newSet = new HashSet<Guid>(newIds);
            var bridges = passages.Where(passage =>
                existingSet.Contains(passage.FirstId) && newSet.Contains(passage.SecondId) ||
                existingSet.Contains(passage.SecondId) && newSet.Contains(passage.FirstId)).ToArray();
            if (bridges.Length != 1)
            {
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "A linear append must contain exactly one old-to-new bridge."));
                return;
            }
            var bridge = bridges[0];
            var oldId = existingSet.Contains(bridge.FirstId) ? bridge.FirstId : bridge.SecondId;
            if (oldId != attachment.Value)
                diagnostics.Add(AttachmentDiagnostic(attachment));
        }

        /// <summary>
        /// Finds the endpoint identifiers of a simple path.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="passages">The passages value.</param>
        /// <returns>The endpoint identifiers of a simple path.</returns>
        private static Guid[] PathEndpoints(IReadOnlyList<Guid> nodes,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            if (nodes.Count == 1) return new[] { nodes[0] };
            var degrees = nodes.ToDictionary(id => id, _ => 0);
            foreach (var passage in passages)
            {
                if (degrees.ContainsKey(passage.FirstId)) degrees[passage.FirstId]++;
                if (degrees.ContainsKey(passage.SecondId)) degrees[passage.SecondId]++;
            }
            return degrees.Where(pair => pair.Value == 1).Select(pair => pair.Key).ToArray();
        }

        /// <summary>
        /// Selects the node used to attach a new topology fragment.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="endpoints">The endpoints value.</param>
        /// <returns>The resulting identifier.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be completed in the current state.</exception>
        private static Guid SelectAttachment(LocationStrategyContext context, IReadOnlyList<Guid> endpoints)
        {
            if (context.AttachmentId.HasValue)
            {
                if (!endpoints.Contains(context.AttachmentId.Value))
                    ThrowIfAny(new[] { AttachmentDiagnostic(context.AttachmentId) });
                return context.AttachmentId.Value;
            }
            var candidates = endpoints.OrderBy(id => id).ToArray();
            var index = context.Random.Next(candidates.Length);
            if (index < 0 || index >= candidates.Length)
                throw new InvalidOperationException("The location random source returned an out-of-range index.");
            return candidates[index];
        }

        /// <summary>
        /// Creates passage proposals for a path through the supplied nodes.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <returns>The resulting collection.</returns>
        private static IEnumerable<LocationPassageProposal> NewPath(IReadOnlyList<Guid> nodes)
        {
            for (var index = 1; index < nodes.Count; index++)
                yield return new LocationPassageProposal(nodes[index - 1], nodes[index]);
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
                "The attachment must be an endpoint of the existing path.",
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
