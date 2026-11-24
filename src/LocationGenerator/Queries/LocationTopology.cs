using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Services.Binds;
using LocationGenerator.Params;

namespace LocationGenerator.Queries
{
    /// <summary>
    /// Provides graph queries for containment and passage relationships between locations.
    /// </summary>
    public static class LocationTopology
    {
        /// <summary>
        /// Gets the locations directly contained by the specified parent.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="parent">The parent element.</param>
        /// <returns>The resulting collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public static IReadOnlyList<IElement> GetChildren(Plot plot, IElement parent)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            return plot.Relations
                .Where(relation => IsActive(relation, BaseRelationKeys.Contains) &&
                                   Same(relation.Source, parent))
                .Select(relation => relation.Target)
                .ToList();
        }

        /// <summary>
        /// Gets the entrance location contained by the specified root.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="root">The root location for the operation.</param>
        /// <returns>The entrance location contained by the specified root.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public static IElement? GetEntrance(Plot plot, IElement root)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (root == null) throw new ArgumentNullException(nameof(root));

            return plot.Relations
                .Where(relation => IsActive(relation, LocationRelationKeys.Entrance) &&
                                   Same(relation.Source, root))
                .Select(relation => relation.Target)
                .FirstOrDefault();
        }

        /// <summary>
        /// Gets the locations connected to the specified location by active passages.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="location">The location to process.</param>
        /// <returns>The resulting collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public static IReadOnlyList<IElement> GetPassageNeighbors(Plot plot, IElement location)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (location == null) throw new ArgumentNullException(nameof(location));

            var neighbors = new List<IElement>();
            foreach (var relation in plot.Relations.Where(relation =>
                         IsActive(relation, BaseRelationKeys.Passage)))
            {
                if (Same(relation.Source, location) && !Same(relation.Target, location))
                    AddUnique(neighbors, relation.Target);
                else if (Same(relation.Target, location) && !Same(relation.Source, location))
                    AddUnique(neighbors, relation.Source);
            }

            return neighbors;
        }

        /// <summary>
        /// Gets all locations reachable from the entrance through active passages.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="entrance">The entrance location from which traversal starts.</param>
        /// <returns>The resulting collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public static IReadOnlyList<IElement> GetReachableLocations(Plot plot, IElement entrance)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (entrance == null) throw new ArgumentNullException(nameof(entrance));

            var reachable = new List<IElement> { entrance };
            var queue = new Queue<IElement>();
            queue.Enqueue(entrance);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in GetPassageNeighbors(plot, current))
                {
                    if (reachable.Any(location => Same(location, neighbor))) continue;
                    reachable.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return reachable;
        }

        /// <summary>
        /// Determines whether the candidate belongs to the subtree rooted at the specified element.
        /// </summary>
        /// <param name="plot">The plot that contains the affected elements.</param>
        /// <param name="root">The root location for the operation.</param>
        /// <param name="candidate">The candidate value to evaluate.</param>
        /// <returns><see langword="true"/> if the candidate belongs to the subtree rooted at the specified element; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public static bool IsInSubtree(Plot plot, IElement root, IElement candidate)
        {
            if (plot == null) throw new ArgumentNullException(nameof(plot));
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));

            var visited = new List<IElement> { root };
            var queue = new Queue<IElement>();
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (Same(current, candidate)) return true;
                foreach (var child in GetChildren(plot, current))
                {
                    if (visited.Any(location => Same(location, child))) continue;
                    visited.Add(child);
                    queue.Enqueue(child);
                }
            }

            return false;
        }

        /// <summary>
        /// Adds the element when an equivalent element is not already present.
        /// </summary>
        /// <param name="values">The values to process.</param>
        /// <param name="value">The value to process.</param>
        private static void AddUnique(ICollection<IElement> values, IElement value)
        {
            if (values.Any(existing => Same(existing, value))) return;
            values.Add(value);
        }

        /// <summary>
        /// Determines whether two element references identify the same element.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <returns><see langword="true"/> if two element references identify the same element; otherwise, <see langword="false"/>.</returns>
        private static bool Same(IElement first, IElement second)
        {
            return first.Id == second.Id;
        }

        /// <summary>
        /// Determines whether the relation is active for the specified key.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <param name="key">The parameter key to use.</param>
        /// <returns><see langword="true"/> if the relation is active for the specified key; otherwise, <see langword="false"/>.</returns>
        private static bool IsActive(Relation relation, IParamKey key)
        {
            return relation.Param.Equals(key) && relation.Value is bool value && value;
        }
    }
}
