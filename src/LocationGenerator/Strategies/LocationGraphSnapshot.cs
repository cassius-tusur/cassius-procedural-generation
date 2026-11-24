using System;
using System.Collections.Generic;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Captures node identifiers and undirected passages in an existing location graph.
    /// </summary>
    public sealed record LocationGraphSnapshot
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LocationGraphSnapshot"/>.
        /// </summary>
        /// <param name="nodeIds">The identifiers of the node values.</param>
        /// <param name="passages">The passages value.</param>
        public LocationGraphSnapshot(IReadOnlyList<Guid> nodeIds,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            NodeIds = Copy(nodeIds, nameof(nodeIds));
            Passages = Copy(passages, nameof(passages));
        }

        public IReadOnlyList<Guid> NodeIds { get; }
        public IReadOnlyList<LocationPassageProposal> Passages { get; }
        /// <summary>
        /// Creates a defensive copy of the supplied collection.
        /// </summary>
        /// <typeparam name="T">The type of value processed by the method.</typeparam>
        /// <param name="values">The values to process.</param>
        /// <param name="name">The name value.</param>
        /// <returns>The resulting collection.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values, string name)
        {
            if (values == null) throw new ArgumentNullException(name);
            return Array.AsReadOnly(new List<T>(values).ToArray());
        }
    }
}
