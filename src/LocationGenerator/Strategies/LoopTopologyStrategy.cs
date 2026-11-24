using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Builds and validates cyclic location topologies with bounded node degree.
    /// </summary>
    public sealed class LoopTopologyStrategy : ILocationTopologyStrategy
    {
        public string StrategyId => "Loop";
        public Type OptionsType => typeof(LoopOptions);

        /// <summary>
        /// Validates options.
        /// </summary>
        /// <param name="options">The generation options.</param>
        public void ValidateOptions(ILocationTopologyOptions options)
        {
            if (!(options is LoopOptions loop))
            {
                ThrowIfAny(new[]
                {
                    new LocationGenerationDiagnostic("options.typeMismatch", "options", "does not match Loop.")
                });
                return;
            }

            var diagnostics = new List<LocationGenerationDiagnostic>();
            if (loop.CycleCount < 0)
                diagnostics.Add(new LocationGenerationDiagnostic("options.invalid", "options.cycleCount",
                    "CycleCount must not be negative."));
            if (loop.MaxDegree < 2)
                diagnostics.Add(new LocationGenerationDiagnostic("options.invalid", "options.maxDegree",
                    "MaxDegree must be at least 2."));
            if (loop.MaxDegree == 2 && loop.CycleCount > 1)
                diagnostics.Add(new LocationGenerationDiagnostic("options.invalid", "options.cycleCount",
                    "A connected graph with MaxDegree 2 cannot contain more than one independent cycle."));
            ThrowIfAny(diagnostics);
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
            var loop = (LoopOptions)options;
            var existing = context.ExistingGraph.NodeIds;
            var newIds = context.NewNodeIds;
            var diagnostics = new List<LocationGenerationDiagnostic>();
            ValidateIds(existing, newIds, diagnostics);
            ValidateGraph(existing, context.ExistingGraph.Passages, loop.MaxDegree, null, "existingGraph", diagnostics);
            EnsureNewNodes(newIds, diagnostics);
            ThrowIfAny(diagnostics);

            Guid? attachment = null;
            if (existing.Count > 0)
                attachment = SelectAttachment(context, Degrees(existing, context.ExistingGraph.Passages), loop.MaxDegree);
            else if (context.AttachmentId.HasValue)
                ThrowIfAny(new[] { AttachmentDiagnostic(context.AttachmentId) });

            var bridgeEndpoint = attachment.HasValue ? newIds[0] : (Guid?)null;
            var passages = new DegreeBoundedGraphBuilder().Build(newIds, loop.CycleCount, loop.MaxDegree,
                bridgeEndpoint, context.Random).ToList();
            if (attachment.HasValue)
                passages.Add(new LocationPassageProposal(attachment.Value, bridgeEndpoint!.Value));
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

            var loop = (LoopOptions)options;
            var existing = context.ExistingGraph.NodeIds;
            var newIds = context.NewNodeIds;
            var proposals = plan.Passages ?? Array.Empty<LocationPassageProposal>();
            var diagnostics = new List<LocationGenerationDiagnostic>();
            ValidateIds(existing, newIds, diagnostics);
            ValidateGraph(existing, context.ExistingGraph.Passages, loop.MaxDegree, null, "existingGraph", diagnostics);
            EnsureNewNodes(newIds, diagnostics);
            ValidateProposalEndpoints(existing, newIds, proposals, diagnostics);

            if (existing.Count == 0)
            {
                if (plan.AttachmentId.HasValue)
                    diagnostics.Add(AttachmentDiagnostic(plan.AttachmentId));
                if (context.AttachmentId.HasValue)
                    diagnostics.Add(AttachmentDiagnostic(context.AttachmentId));
                if (proposals.Any(edge => !newIds.Contains(edge.FirstId) || !newIds.Contains(edge.SecondId)))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                        "A new Loop plan may contain only edges between new nodes."));
            }
            else
            {
                ValidateAppendBridge(existing, newIds, context.AttachmentId, plan.AttachmentId, proposals, diagnostics);
            }

            long expectedProposalCount = (long)newIds.Count - 1L + loop.CycleCount +
                (existing.Count > 0 ? 1L : 0L);
            if (proposals.Count != expectedProposalCount)
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "The Loop plan does not contain the requested number of internal edges and one append bridge."));

            var combinedNodes = existing.Concat(newIds).ToArray();
            var combinedPassages = context.ExistingGraph.Passages.Concat(proposals).ToArray();
            long expectedCombinedEdges = (long)context.ExistingGraph.Passages.Count + expectedProposalCount;
            if (combinedNodes.Length == combinedNodes.Distinct().Count())
                ValidateGraph(combinedNodes, combinedPassages, loop.MaxDegree, expectedCombinedEdges,
                    "plan", diagnostics);
            ThrowIfAny(diagnostics);
        }

        /// <summary>
        /// Selects the node used to attach a new topology fragment.
        /// </summary>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="degrees">The degrees value.</param>
        /// <param name="maxDegree">The max degree value.</param>
        /// <returns>The resulting identifier.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be completed in the current state.</exception>
        private static Guid SelectAttachment(LocationStrategyContext context,
            IReadOnlyDictionary<Guid, int> degrees, int maxDegree)
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
            if (candidates.Length == 0)
                ThrowIfAny(new[] { AttachmentDiagnostic(null) });
            var index = context.Random.Next(candidates.Length);
            if (index < 0 || index >= candidates.Length)
                throw new InvalidOperationException("The location random source returned an out-of-range index.");
            return candidates[index];
        }

        /// <summary>
        /// Validates append bridge.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="requestedAttachment">The requested attachment value.</param>
        /// <param name="declaredAttachment">The declared attachment value.</param>
        /// <param name="proposals">The proposals value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateAppendBridge(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            Guid? requestedAttachment, Guid? declaredAttachment,
            IReadOnlyList<LocationPassageProposal> proposals,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var existingSet = new HashSet<Guid>(existing);
            var newSet = new HashSet<Guid>(newIds);
            var bridges = proposals.Where(edge =>
                existingSet.Contains(edge.FirstId) && newSet.Contains(edge.SecondId) ||
                existingSet.Contains(edge.SecondId) && newSet.Contains(edge.FirstId)).ToArray();
            if (!declaredAttachment.HasValue || !existingSet.Contains(declaredAttachment.Value))
                diagnostics.Add(AttachmentDiagnostic(declaredAttachment));
            if (requestedAttachment.HasValue && requestedAttachment != declaredAttachment)
                diagnostics.Add(AttachmentDiagnostic(requestedAttachment));
            if (bridges.Length != 1)
            {
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                    "A Loop append must contain exactly one old-to-new bridge."));
            }
            else
            {
                var actualOldEndpoint = existingSet.Contains(bridges[0].FirstId)
                    ? bridges[0].FirstId
                    : bridges[0].SecondId;
                if (!declaredAttachment.HasValue || actualOldEndpoint != declaredAttachment.Value)
                    diagnostics.Add(AttachmentDiagnostic(declaredAttachment));
            }

            foreach (var proposal in proposals)
            {
                var firstOld = existingSet.Contains(proposal.FirstId);
                var secondOld = existingSet.Contains(proposal.SecondId);
                if (firstOld && secondOld)
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                        "A Loop append must not add edges between existing nodes."));
            }
        }

        /// <summary>
        /// Validates graph.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="maxDegree">The max degree value.</param>
        /// <param name="expectedEdges">The expected edges value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateGraph(IReadOnlyList<Guid> nodes,
            IReadOnlyList<LocationPassageProposal> passages, int maxDegree, long? expectedEdges,
            string path, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (nodes.Count == 0)
            {
                if (passages.Count > 0)
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", path + ".passages",
                        "Passages cannot exist without nodes."));
                return;
            }

            var nodeSet = new HashSet<Guid>(nodes);
            if (nodeSet.Count != nodes.Count)
            {
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", path + ".nodeIds",
                    "Node IDs must be unique."));
                return;
            }

            var degrees = nodes.ToDictionary(id => id, _ => 0);
            var edges = new HashSet<(Guid First, Guid Second)>();
            foreach (var passage in passages)
            {
                if (passage.FirstId == passage.SecondId || !nodeSet.Contains(passage.FirstId) ||
                    !nodeSet.Contains(passage.SecondId))
                {
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", path + ".passages",
                        "Passages must join distinct known nodes.", new[] { passage.FirstId, passage.SecondId }));
                    continue;
                }
                if (!edges.Add(Canonical(passage.FirstId, passage.SecondId)))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", path + ".passages",
                        "Passages must be unique.", new[] { passage.FirstId, passage.SecondId }));
                degrees[passage.FirstId]++;
                degrees[passage.SecondId]++;
            }

            if (expectedEdges.HasValue && passages.Count != expectedEdges.Value)
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", path + ".passages",
                    "The graph has an unexpected edge count."));
            if (degrees.Values.Any(degree => degree > maxDegree))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", path + ".passages",
                    "The graph exceeds MaxDegree."));
            if (!Connected(nodes, edges))
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", path + ".passages",
                    "The graph must be connected."));
        }

        /// <summary>
        /// Validates proposal endpoints.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="proposals">The proposals value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateProposalEndpoints(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            IReadOnlyList<LocationPassageProposal> proposals,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var allowed = new HashSet<Guid>(existing);
            allowed.UnionWith(newIds);
            foreach (var proposal in proposals)
                if (!allowed.Contains(proposal.FirstId) || !allowed.Contains(proposal.SecondId))
                    diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "plan.passages",
                        "Plan passages must reference existing or newly created nodes.",
                        new[] { proposal.FirstId, proposal.SecondId }));
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
        /// Validates ids.
        /// </summary>
        /// <param name="existing">The existing value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateIds(IReadOnlyList<Guid> existing, IReadOnlyList<Guid> newIds,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (existing.Count != existing.Distinct().Count())
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "existingGraph.nodeIds",
                    "Existing node IDs must be unique."));
            if (newIds.Count != newIds.Distinct().Count())
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "newNodeIds",
                    "New node IDs must be unique."));
            var overlap = new HashSet<Guid>(existing);
            overlap.IntersectWith(newIds);
            if (overlap.Count > 0)
                diagnostics.Add(new LocationGenerationDiagnostic("graph.incompatible", "newNodeIds",
                    "New node IDs must not overlap existing IDs.", overlap.ToArray()));
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
                diagnostics.Add(new LocationGenerationDiagnostic("nodeCount.invalid", "nodeCount",
                    "At least one new node is required."));
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
                "The attachment must identify an existing node with free degree and match the plan bridge.",
                id.HasValue ? new[] { id.Value } : Array.Empty<Guid>());

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
