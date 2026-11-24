using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Builds connected undirected graphs subject to degree and edge constraints.
    /// </summary>
    public sealed class DegreeBoundedGraphBuilder
    {
        /// <summary>
        /// Builds a connected edge set that satisfies the requested degree bounds.
        /// </summary>
        /// <param name="nodes">The nodes value.</param>
        /// <param name="cycleCount">The cycle count value.</param>
        /// <param name="maxDegree">The max degree value.</param>
        /// <param name="reservedAttachmentId">The identifier of the reserved attachment.</param>
        /// <param name="random">The random source used to make deterministic choices.</param>
        /// <returns>The resulting collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public IReadOnlyList<LocationPassageProposal> Build(IReadOnlyList<Guid> nodes, int cycleCount,
            int maxDegree, Guid? reservedAttachmentId, ILocationRandom random)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (random == null) throw new ArgumentNullException(nameof(random));

            var diagnostics = new List<LocationGenerationDiagnostic>();
            if (nodes.Count == 0)
                diagnostics.Add(Incompatible("At least one node is required."));
            if (nodes.Count != nodes.Distinct().Count())
                diagnostics.Add(Incompatible("Node IDs must be unique."));
            if (cycleCount < 0)
                diagnostics.Add(Incompatible("CycleCount must not be negative."));
            if (maxDegree < 2)
                diagnostics.Add(Incompatible("MaxDegree must be at least 2."));

            var reservedIndex = -1;
            if (reservedAttachmentId.HasValue)
            {
                for (var index = 0; index < nodes.Count; index++)
                    if (nodes[index] == reservedAttachmentId.Value)
                    {
                        reservedIndex = index;
                        break;
                    }
                if (reservedIndex < 0)
                    diagnostics.Add(Incompatible("The reserved attachment must identify an input node."));
            }

            long targetEdges = -1;
            long simpleEdgeLimit = -1;
            long capacitySum = 0;
            if (diagnostics.Count == 0)
            {
                targetEdges = (long)nodes.Count - 1L + cycleCount;
                simpleEdgeLimit = (long)nodes.Count * (nodes.Count - 1L) / 2L;
                for (var index = 0; index < nodes.Count; index++)
                    capacitySum += maxDegree - (index == reservedIndex ? 1L : 0L);

                if (targetEdges < nodes.Count - 1L || targetEdges > simpleEdgeLimit ||
                    targetEdges > capacitySum / 2L || targetEdges > int.MaxValue)
                    diagnostics.Add(Incompatible("The requested edge count cannot satisfy the simple degree-bounded graph constraints."));
                if (nodes.Count > 1 && Enumerable.Range(0, nodes.Count)
                        .Any(index => maxDegree - (index == reservedIndex ? 1 : 0) < 1))
                    diagnostics.Add(Incompatible("Every node needs capacity for a connected graph."));
            }

            ThrowIfAny(diagnostics);

            var capacities = new int[nodes.Count];
            for (var index = 0; index < capacities.Length; index++)
                capacities[index] = maxDegree - (index == reservedIndex ? 1 : 0);

            var nodeIndexes = nodes.Select((id, index) => new { id, index })
                .ToDictionary(item => item.id, item => item.index);
            var tree = DegreeBoundedTreeBuilder.Build(nodes, maxDegree, reservedAttachmentId, random);
            var treeEdges = tree.Select(passage =>
                new Edge(nodeIndexes[passage.FirstId], nodeIndexes[passage.SecondId])).ToList();
            var treeEdgeSet = new HashSet<(int First, int Second)>(treeEdges.Select(edge =>
                edge.First < edge.Second ? (edge.First, edge.Second) : (edge.Second, edge.First)));
            var remainingCandidates = new List<Edge>();
            for (var first = 0; first < nodes.Count; first++)
                for (var second = first + 1; second < nodes.Count; second++)
                    if (!treeEdgeSet.Contains((first, second)))
                        remainingCandidates.Add(new Edge(first, second));
            Shuffle(remainingCandidates, random);

            var candidates = new List<Edge>(treeEdges.Count + remainingCandidates.Count);
            candidates.AddRange(treeEdges);
            candidates.AddRange(remainingCandidates);

            var search = new Search(nodes.Count, capacities, candidates, (int)targetEdges);
            if (!search.TryFind(out var selected))
                ThrowIfAny(new[] { Incompatible("No connected simple graph satisfies the requested edge count and degree bounds.") });

            return Array.AsReadOnly(selected
                .Select(edge => new LocationPassageProposal(nodes[edge.First], nodes[edge.Second]))
                .ToArray());
        }

        /// <summary>
        /// Shuffles the supplied value.
        /// </summary>
        /// <param name="edges">The edges value.</param>
        /// <param name="random">The random source used to make deterministic choices.</param>
        /// <exception cref="InvalidOperationException">Thrown when the operation cannot be completed in the current state.</exception>
        private static void Shuffle(IList<Edge> edges, ILocationRandom random)
        {
            for (var index = edges.Count - 1; index > 0; index--)
            {
                var selected = random.Next(index + 1);
                if (selected < 0 || selected > index)
                    throw new InvalidOperationException("The location random source returned an out-of-range index.");
                if (selected == index) continue;
                var temporary = edges[index];
                edges[index] = edges[selected];
                edges[selected] = temporary;
            }
        }

        /// <summary>
        /// Creates a diagnostic describing incompatible graph constraints.
        /// </summary>
        /// <param name="message">The message value.</param>
        /// <returns>A diagnostic describing the incompatible graph constraints.</returns>
        private static LocationGenerationDiagnostic Incompatible(string message) =>
            new LocationGenerationDiagnostic("graph.incompatible", "graph", message);

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
        /// Represents an undirected candidate edge between two topology nodes.
        /// </summary>
        private readonly struct Edge
        {
            /// <summary>
            /// Initializes a new instance of <see cref="Edge"/>.
            /// </summary>
            /// <param name="first">The first value.</param>
            /// <param name="second">The second value.</param>
            public Edge(int first, int second)
            {
                First = first;
                Second = second;
            }

            public int First { get; }
            public int Second { get; }
        }

        /// <summary>
        /// Searches for an edge selection that satisfies graph constraints.
        /// </summary>
        private sealed class Search
        {
            private readonly int _nodeCount;
            private readonly int[] _capacities;
            private readonly IReadOnlyList<Edge> _candidates;
            private readonly int _targetEdges;
            private readonly int[] _degrees;
            private readonly List<Edge> _selected = new List<Edge>();

            /// <summary>
            /// Initializes a new instance of <see cref="Search"/>.
            /// </summary>
            /// <param name="nodeCount">The node count value.</param>
            /// <param name="capacities">The capacities value.</param>
            /// <param name="candidates">The candidates value.</param>
            /// <param name="targetEdges">The target edges value.</param>
            public Search(int nodeCount, int[] capacities, IReadOnlyList<Edge> candidates, int targetEdges)
            {
                _nodeCount = nodeCount;
                _capacities = capacities;
                _candidates = candidates;
                _targetEdges = targetEdges;
                _degrees = new int[nodeCount];
            }

            /// <summary>
            /// Attempts to find an edge selection that satisfies all graph constraints.
            /// </summary>
            /// <param name="selected">The selected value.</param>
            /// <returns><see langword="true"/> if a valid edge selection was found; otherwise, <see langword="false"/>.</returns>
            public bool TryFind(out IReadOnlyList<Edge> selected)
            {
                var found = Visit(0);
                selected = found ? _selected.ToArray() : Array.Empty<Edge>();
                return found;
            }

            /// <summary>
            /// Explores the next candidate edge during the constrained graph search.
            /// </summary>
            /// <param name="candidateIndex">The candidate index value.</param>
            /// <returns><see langword="true"/> if the search can complete from the candidate state; otherwise, <see langword="false"/>.</returns>
            private bool Visit(int candidateIndex)
            {
                var needed = _targetEdges - _selected.Count;
                if (needed == 0) return IsConnected(_selected);
                if (candidateIndex >= _candidates.Count || _candidates.Count - candidateIndex < needed)
                    return false;

                var viableCount = 0;
                var incidentCounts = new int[_nodeCount];
                var reachableEdges = new List<Edge>(_selected);
                for (var index = candidateIndex; index < _candidates.Count; index++)
                {
                    var edge = _candidates[index];
                    if (_degrees[edge.First] >= _capacities[edge.First] ||
                        _degrees[edge.Second] >= _capacities[edge.Second])
                        continue;
                    viableCount++;
                    incidentCounts[edge.First]++;
                    incidentCounts[edge.Second]++;
                    reachableEdges.Add(edge);
                }
                if (viableCount < needed || !IsConnected(reachableEdges)) return false;

                long usableDegree = 0;
                for (var node = 0; node < _nodeCount; node++)
                    usableDegree += Math.Min(_capacities[node] - _degrees[node], incidentCounts[node]);
                if (usableDegree / 2L < needed) return false;

                var components = ComponentCount(_selected);
                if (components - 1 > needed) return false;

                var candidate = _candidates[candidateIndex];
                if (_degrees[candidate.First] < _capacities[candidate.First] &&
                    _degrees[candidate.Second] < _capacities[candidate.Second])
                {
                    _selected.Add(candidate);
                    _degrees[candidate.First]++;
                    _degrees[candidate.Second]++;
                    if (Visit(candidateIndex + 1)) return true;
                    _degrees[candidate.Second]--;
                    _degrees[candidate.First]--;
                    _selected.RemoveAt(_selected.Count - 1);
                }

                return Visit(candidateIndex + 1);
            }

            /// <summary>
            /// Counts the connected components formed by the supplied edges.
            /// </summary>
            /// <param name="edges">The edges value.</param>
            /// <returns>The computed value.</returns>
            private int ComponentCount(IReadOnlyList<Edge> edges)
            {
                var union = new UnionFind(_nodeCount);
                foreach (var edge in edges) union.Join(edge.First, edge.Second);
                var roots = new HashSet<int>();
                for (var node = 0; node < _nodeCount; node++) roots.Add(union.Find(node));
                return roots.Count;
            }

            /// <summary>
            /// Determines whether the supplied edges connect every node.
            /// </summary>
            /// <param name="edges">The edges value.</param>
            /// <returns><see langword="true"/> if the supplied edges connect every node; otherwise, <see langword="false"/>.</returns>
            private bool IsConnected(IReadOnlyList<Edge> edges)
            {
                if (_nodeCount <= 1) return true;
                var union = new UnionFind(_nodeCount);
                foreach (var edge in edges) union.Join(edge.First, edge.Second);
                var root = union.Find(0);
                for (var node = 1; node < _nodeCount; node++)
                    if (union.Find(node) != root) return false;
                return true;
            }
        }

        /// <summary>
        /// Tracks connected components during constrained graph construction.
        /// </summary>
        private sealed class UnionFind
        {
            private readonly int[] _parents;

            /// <summary>
            /// Initializes a new instance of <see cref="UnionFind"/>.
            /// </summary>
            /// <param name="count">The count value.</param>
            public UnionFind(int count)
            {
                _parents = new int[count];
                for (var index = 0; index < count; index++) _parents[index] = index;
            }

            /// <summary>
            /// Finds the representative of the set containing the specified value.
            /// </summary>
            /// <param name="value">The value to process.</param>
            /// <returns>The computed value.</returns>
            public int Find(int value)
            {
                while (_parents[value] != value)
                {
                    _parents[value] = _parents[_parents[value]];
                    value = _parents[value];
                }
                return value;
            }

            /// <summary>
            /// Joins the supplied value.
            /// </summary>
            /// <param name="first">The first value.</param>
            /// <param name="second">The second value.</param>
            public void Join(int first, int second)
            {
                first = Find(first);
                second = Find(second);
                if (first != second) _parents[second] = first;
            }
        }
    }
}
