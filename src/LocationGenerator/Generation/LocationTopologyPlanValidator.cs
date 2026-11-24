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
    /// Validates topology proposals and assembled mutation plans before commit.
    /// </summary>
    internal sealed class LocationTopologyPlanValidator
    {
        /// <summary>
        /// Validates a complete location mutation plan before it is committed.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="scope">The scope value.</param>
        /// <param name="request">The topology generation request.</param>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="proposal">The proposal value.</param>
        /// <param name="strategy">The strategy value.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void Validate(Plot plot, LocationGenerationScope scope, LocationTopologyRequest request,
            LocationStrategyContext context, LocationStrategyPlan proposal,
            ILocationTopologyStrategy strategy, LocationMutationPlan plan)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (proposal == null) throw new ArgumentNullException(nameof(proposal));
            if (strategy == null) throw new ArgumentNullException(nameof(strategy));
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var diagnostics = new List<LocationGenerationDiagnostic>();
            var newIds = ValidateLocations(plot, scope, request, context, plan, diagnostics);
            ValidateMutations(plot, plan, newIds, diagnostics);
            ValidateProposal(scope, context, proposal, newIds, diagnostics);
            ValidateContainment(scope, plan, newIds, diagnostics);
            ThrowIfAny(diagnostics);

            // Passage proposals are logical undirected edges. Materialize them on the
            // detached mutation plan before validating the exact reciprocal relation pairs.
            if (!PassageRelations(plan).Any())
            {
                var locations = scope.ExistingLocations.Values
                    .Concat(plan.NewLocations)
                    .GroupBy(location => location.Id)
                    .ToDictionary(group => group.Key, group => group.First());
                foreach (var passage in proposal.Passages)
                    plan.AddPassage(locations[passage.FirstId], locations[passage.SecondId], BaseRelationKeys.Passage);
            }

            ValidateAssembledPassages(plan, proposal, diagnostics);
            ThrowIfAny(diagnostics);

            // Strategy validation is deliberately a separate step. Custom strategies cannot
            // weaken or replace any of the generic plan checks above.
            strategy.ValidatePlan(context, request.Options, proposal);

        }

        /// <summary>
        /// Validates locations.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="scope">The scope value.</param>
        /// <param name="request">The topology generation request.</param>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        /// <returns>The identifiers of the validated new locations.</returns>
        private static HashSet<Guid> ValidateLocations(Plot plot, LocationGenerationScope scope,
            LocationTopologyRequest request, LocationStrategyContext context, LocationMutationPlan plan,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var newIds = new HashSet<Guid>();
            if (plan.NewLocations.Count != request.NodeCount)
                diagnostics.Add(Diagnostic("nodeCount.invalid", "plan.newLocations",
                    "The plan must contain exactly the requested number of new locations."));

            foreach (var location in plan.NewLocations)
            {
                if (location.Type != ElemType.Location)
                    diagnostics.Add(Diagnostic("plan.location.type", "plan.newLocations",
                        "Every new element must be a Location.", location.Id));
                if (!newIds.Add(location.Id))
                    diagnostics.Add(Diagnostic("plan.location.duplicate", "plan.newLocations",
                        "New location IDs must be unique.", location.Id));
                if (plot.Elements.Any(existing => existing.Id == location.Id))
                    diagnostics.Add(Diagnostic("plan.location.collision", "plan.newLocations",
                        "New location IDs must not collide with any existing Plot element.", location.Id));
            }

            var contextIds = new HashSet<Guid>(context.NewNodeIds);
            if (!contextIds.SetEquals(newIds) || context.NewNodeIds.Count != newIds.Count)
                diagnostics.Add(Diagnostic("plan.contextMismatch", "context.newNodeIds",
                    "Strategy context IDs must exactly match the new locations in the mutation plan."));

            var removedIds = new HashSet<Guid>(scope.RemovedLocations.Select(location => location.Id));
            var plannedRemovedIds = new HashSet<Guid>(plan.RemovedLocations.Select(location => location.Id));
            if (!plannedRemovedIds.SetEquals(removedIds) || plan.RemovedLocations.Count != removedIds.Count)
                diagnostics.Add(Diagnostic("plan.removalMismatch", "plan.removedLocations",
                    "The mutation plan must remove exactly the locations selected by the inspected scope."));

            if (plan.RemovedRelations.Count != scope.RemovedRelations.Count ||
                scope.RemovedRelations.Any(expected =>
                    !plan.RemovedRelations.Any(actual => ReferenceEquals(actual, expected))))
                diagnostics.Add(Diagnostic("plan.removalMismatch", "plan.removedRelations",
                    "The mutation plan must remove exactly the relations selected by the inspected scope."));
            return newIds;
        }

        /// <summary>
        /// Validates mutations.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateMutations(Plot plot, LocationMutationPlan plan, ISet<Guid> newIds,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var removedIds = new HashSet<Guid>(plan.RemovedLocations.Select(location => location.Id));
            var retainedIds = new HashSet<Guid>(plot.Elements
                .Where(element => !removedIds.Contains(element.Id)).Select(element => element.Id));
            retainedIds.UnionWith(newIds);
            foreach (var relation in plan.Relations)
            {
                foreach (var id in new[] { relation.Source.Id, relation.Target.Id })
                {
                    if (removedIds.Contains(id))
                        diagnostics.Add(Diagnostic("plan.removedReference", "plan.relations",
                            "New relations must not reference removed locations.", id));
                    else if (!retainedIds.Contains(id))
                        diagnostics.Add(Diagnostic("plan.endpoint.unknown", "plan.relations",
                            "Every new relation endpoint must remain in the Plot.", id));
                }
            }
        }

        /// <summary>
        /// Validates proposal.
        /// </summary>
        /// <param name="scope">The scope value.</param>
        /// <param name="context">The strategy execution context.</param>
        /// <param name="proposal">The proposal value.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateProposal(LocationGenerationScope scope, LocationStrategyContext context,
            LocationStrategyPlan proposal, ISet<Guid> newIds,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var oldIds = new HashSet<Guid>(scope.ExistingGraph.NodeIds);
            var removedIds = new HashSet<Guid>(scope.RemovedLocations.Select(location => location.Id));
            var knownIds = new HashSet<Guid>(oldIds);
            knownIds.UnionWith(newIds);
            var edges = new HashSet<(Guid First, Guid Second)>();
            var bridges = new List<LocationPassageProposal>();

            foreach (var passage in proposal.Passages)
            {
                if (removedIds.Contains(passage.FirstId) || removedIds.Contains(passage.SecondId))
                {
                    var ids = new[] { passage.FirstId, passage.SecondId }.Where(removedIds.Contains).Distinct().ToArray();
                    diagnostics.Add(Diagnostic("plan.removedReference", "plan.passages",
                        "Passage proposals must not reference removed locations.", ids));
                }
                foreach (var id in new[] { passage.FirstId, passage.SecondId })
                {
                    if (!knownIds.Contains(id) && !removedIds.Contains(id))
                        diagnostics.Add(Diagnostic("plan.endpoint.unknown", "plan.passages",
                            "Every Passage endpoint must belong to the local plan.", id));
                }
                if (passage.FirstId == passage.SecondId)
                    diagnostics.Add(Diagnostic("plan.passage.loop", "plan.passages",
                        "Passage proposals cannot be loops.", passage.FirstId));

                var pair = Canonical(passage.FirstId, passage.SecondId);
                if (!edges.Add(pair))
                    diagnostics.Add(Diagnostic("plan.passage.duplicate", "plan.passages",
                        "Passage proposals must be unique in either direction.", passage.FirstId, passage.SecondId));

                var firstOld = oldIds.Contains(passage.FirstId);
                var secondOld = oldIds.Contains(passage.SecondId);
                if (firstOld && secondOld)
                    diagnostics.Add(Diagnostic("plan.oldEdge", "plan.passages",
                        "A topology proposal must not modify edges between existing locations.", passage.FirstId, passage.SecondId));
                if (firstOld != secondOld &&
                    (newIds.Contains(passage.FirstId) || newIds.Contains(passage.SecondId)))
                    bridges.Add(passage);
            }

            if (oldIds.Count == 0)
            {
                if (bridges.Count != 0 || proposal.AttachmentId.HasValue)
                    diagnostics.Add(Diagnostic("attachment.invalid", "plan.attachmentId",
                        "A fresh graph cannot have an attachment."));
            }
            else
            {
                if (bridges.Count != 1)
                    diagnostics.Add(Diagnostic("plan.bridge.invalid", "plan.passages",
                        "Append requires exactly one bridge from the existing graph to the new fragment."));
                if (!proposal.AttachmentId.HasValue || bridges.Count != 1 ||
                    !IsOldBridgeEndpoint(bridges[0], proposal.AttachmentId.Value, oldIds, newIds))
                    diagnostics.Add(Diagnostic("attachment.invalid", "plan.attachmentId",
                        "AttachmentId must be the existing endpoint of the single append bridge.",
                        proposal.AttachmentId.HasValue ? new[] { proposal.AttachmentId.Value } : Array.Empty<Guid>()));
                if (context.AttachmentId.HasValue && proposal.AttachmentId != context.AttachmentId)
                    diagnostics.Add(Diagnostic("attachment.invalid", "plan.attachmentId",
                        "The selected attachment must match the requested attachment.", context.AttachmentId.Value));
            }

            ValidateConnected(oldIds, newIds, scope.ExistingGraph.Passages.Concat(proposal.Passages), diagnostics);
        }

        /// <summary>
        /// Validates connected.
        /// </summary>
        /// <param name="oldIds">The identifiers of the old values.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="passages">The passages value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateConnected(ISet<Guid> oldIds, ISet<Guid> newIds,
            IEnumerable<LocationPassageProposal> passages, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var nodes = new HashSet<Guid>(oldIds);
            nodes.UnionWith(newIds);
            if (nodes.Count == 0) return;
            var adjacency = nodes.ToDictionary(id => id, _ => new List<Guid>());
            foreach (var passage in passages)
            {
                if (!nodes.Contains(passage.FirstId) || !nodes.Contains(passage.SecondId) || passage.FirstId == passage.SecondId)
                    continue;
                adjacency[passage.FirstId].Add(passage.SecondId);
                adjacency[passage.SecondId].Add(passage.FirstId);
            }
            var visited = new HashSet<Guid>();
            var pending = new Queue<Guid>();
            pending.Enqueue(nodes.First());
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                foreach (var neighbor in adjacency[current]) pending.Enqueue(neighbor);
            }
            if (visited.Count != nodes.Count)
                diagnostics.Add(Diagnostic("graph.disconnected", "plan.passages",
                    "The resulting local Passage graph must be connected.", nodes.Except(visited).ToArray()));
        }

        /// <summary>
        /// Validates containment.
        /// </summary>
        /// <param name="scope">The scope value.</param>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateContainment(LocationGenerationScope scope, LocationMutationPlan plan,
            ISet<Guid> newIds, ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            foreach (var id in newIds)
            {
                var active = plan.Relations.Where(relation => IsTrue(relation) &&
                    relation.Param.Equals(BaseRelationKeys.Contains) && relation.Target.Id == id).ToList();
                if (active.Count != 1 || active[0].Source.Id != scope.Root.Id)
                    diagnostics.Add(Diagnostic("plan.contains.invalid", "plan.relations",
                        "Every new location must have exactly one active Root-to-child Contains relation.", id));
            }
        }

        /// <summary>
        /// Validates assembled passages.
        /// </summary>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <param name="proposal">The proposal value.</param>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        private static void ValidateAssembledPassages(LocationMutationPlan plan, LocationStrategyPlan proposal,
            ICollection<LocationGenerationDiagnostic> diagnostics)
        {
            var relations = PassageRelations(plan).ToList();
            if (relations.Count == 0) return;
            var expected = new HashSet<(Guid Source, Guid Target)>();
            foreach (var passage in proposal.Passages)
            {
                expected.Add((passage.FirstId, passage.SecondId));
                expected.Add((passage.SecondId, passage.FirstId));
            }
            var actual = new HashSet<(Guid Source, Guid Target)>();
            foreach (var relation in relations)
            {
                if (!(relation.Value is bool value) || !value)
                {
                    diagnostics.Add(Diagnostic("plan.passage.unpaired", "plan.relations",
                        "Every assembled Passage relation must have the Boolean value true.",
                        relation.Source.Id, relation.Target.Id));
                    continue;
                }
                if (!actual.Add((relation.Source.Id, relation.Target.Id)))
                    diagnostics.Add(Diagnostic("plan.passage.duplicate", "plan.relations",
                        "Assembled directed Passage relations must be unique.", relation.Source.Id, relation.Target.Id));
            }
            if (relations.Count != expected.Count || !actual.SetEquals(expected))
                diagnostics.Add(Diagnostic("plan.passage.unpaired", "plan.relations",
                    "Each logical Passage proposal must be materialized as exactly two reciprocal directed relations."));
        }

        /// <summary>
        /// Returns the passage relations added by the mutation plan.
        /// </summary>
        /// <param name="plan">The mutation or strategy plan to process.</param>
        /// <returns>The resulting collection.</returns>
        private static IEnumerable<Relation> PassageRelations(LocationMutationPlan plan) =>
            plan.Relations.Where(relation => relation.Param.Equals(BaseRelationKeys.Passage));

        /// <summary>
        /// Determines whether the relation stores an active boolean value.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <returns><see langword="true"/> if the relation stores an active boolean value; otherwise, <see langword="false"/>.</returns>
        private static bool IsTrue(Relation relation) => relation.Value is bool value && value;

        /// <summary>
        /// Returns the identifiers in canonical order.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns>The identifiers in canonical order.</returns>
        private static (Guid First, Guid Second) Canonical(Guid first, Guid second) =>
            first.CompareTo(second) <= 0 ? (first, second) : (second, first);

        /// <summary>
        /// Determines whether the identifier is an endpoint of an existing bridge passage.
        /// </summary>
        /// <param name="passage">The passage value.</param>
        /// <param name="attachment">The attachment value.</param>
        /// <param name="oldIds">The identifiers of the old values.</param>
        /// <param name="newIds">The identifiers of the new values.</param>
        /// <returns><see langword="true"/> if the identifier is an endpoint of an existing bridge passage; otherwise, <see langword="false"/>.</returns>
        private static bool IsOldBridgeEndpoint(LocationPassageProposal passage, Guid attachment,
            ISet<Guid> oldIds, ISet<Guid> newIds) =>
            passage.FirstId == attachment && oldIds.Contains(passage.FirstId) && newIds.Contains(passage.SecondId) ||
            passage.SecondId == attachment && oldIds.Contains(passage.SecondId) && newIds.Contains(passage.FirstId);

        /// <summary>
        /// Creates a location-generation diagnostic with the supplied context.
        /// </summary>
        /// <param name="code">The code value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="message">The message value.</param>
        /// <param name="ids">The ids value.</param>
        /// <returns>The generated diagnostic.</returns>
        private static LocationGenerationDiagnostic Diagnostic(string code, string path, string message,
            params Guid[] ids) => new LocationGenerationDiagnostic(code, path, message, ids);

        /// <summary>
        /// Throws a validation exception when diagnostics are present.
        /// </summary>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        /// <exception cref="LocationGenerationValidationException">Thrown when the location-generation request or result is invalid.</exception>
        private static void ThrowIfAny(IReadOnlyCollection<LocationGenerationDiagnostic> diagnostics)
        {
            if (diagnostics.Count > 0)
                throw new LocationGenerationValidationException(diagnostics.ToArray());
        }
    }
}
