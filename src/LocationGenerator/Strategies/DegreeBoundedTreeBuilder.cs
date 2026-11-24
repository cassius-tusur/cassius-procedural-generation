using System;
using System.Collections.Generic;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Builds a random tree by attaching each node to an earlier node with remaining degree capacity.
    /// </summary>
    internal static class DegreeBoundedTreeBuilder
    {
        private const int RecentGenerationCount = 3;
        private const int LeafSelectionWeight = 1;

        /// <summary>
        /// Builds a random degree-bounded tree over the supplied node identifiers.
        /// </summary>
        /// <param name="nodes">The node identifiers in creation order.</param>
        /// <param name="maxDegree">The maximum permitted degree of each node.</param>
        /// <param name="reservedAttachmentId">The optional node that must retain capacity for an external attachment.</param>
        /// <param name="random">The random source used to select parent nodes.</param>
        /// <returns>The generated undirected passage proposals.</returns>
        /// <exception cref="ArgumentException">Thrown when the reserved attachment is not present in <paramref name="nodes"/>.</exception>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no valid parent exists or the random source returns an invalid index.</exception>
        public static IReadOnlyList<LocationPassageProposal> Build(IReadOnlyList<Guid> nodes, int maxDegree,
            Guid? reservedAttachmentId, ILocationRandom random)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (random == null) throw new ArgumentNullException(nameof(random));

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
                    throw new ArgumentException("The reserved attachment must identify an input node.",
                        nameof(reservedAttachmentId));
            }

            var degrees = new int[nodes.Count];
            var depths = new int[nodes.Count];
            var childCounts = new int[nodes.Count];
            var passages = new List<LocationPassageProposal>(Math.Max(0, nodes.Count - 1));
            var candidates = new List<int>();
            var weightedCandidates = new List<int>();
            var maximumDepth = 0;
            for (var node = 1; node < nodes.Count; node++)
            {
                candidates.Clear();
                var minimumPreferredDepth = Math.Max(0, maximumDepth - RecentGenerationCount + 1);
                for (var candidate = 0; candidate < node; candidate++)
                {
                    var capacity = maxDegree - (candidate == reservedIndex ? 1 : 0);
                    if (degrees[candidate] < capacity && depths[candidate] >= minimumPreferredDepth)
                        candidates.Add(candidate);
                }

                if (candidates.Count == 0)
                {
                    for (var candidate = 0; candidate < node; candidate++)
                    {
                        var capacity = maxDegree - (candidate == reservedIndex ? 1 : 0);
                        if (degrees[candidate] < capacity) candidates.Add(candidate);
                    }
                }

                if (candidates.Count == 0)
                    throw new InvalidOperationException("No created node has remaining degree capacity.");

                var parent = candidates[0];
                if (candidates.Count > 1)
                {
                    weightedCandidates.Clear();
                    foreach (var candidate in candidates)
                    {
                        var weight = childCounts[candidate] == 0 ? LeafSelectionWeight : 1;
                        for (var occurrence = 0; occurrence < weight; occurrence++)
                            weightedCandidates.Add(candidate);
                    }

                    var selected = random.Next(weightedCandidates.Count);
                    if (selected < 0 || selected >= weightedCandidates.Count)
                        throw new InvalidOperationException("The location random source returned an out-of-range index.");
                    parent = weightedCandidates[selected];
                }

                if (parent < 0 || parent >= node)
                    throw new InvalidOperationException("The location random source returned an out-of-range index.");

                passages.Add(new LocationPassageProposal(nodes[parent], nodes[node]));
                degrees[parent]++;
                degrees[node]++;
                childCounts[parent]++;
                depths[node] = depths[parent] + 1;
                maximumDepth = Math.Max(maximumDepth, depths[node]);
            }

            return Array.AsReadOnly(passages.ToArray());
        }
    }
}
