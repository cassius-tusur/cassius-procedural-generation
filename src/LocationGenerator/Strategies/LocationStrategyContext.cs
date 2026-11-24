using System;
using System.Collections.Generic;
using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Supplies existing graph state, new node identifiers, attachment, and randomness to a strategy.
    /// </summary>
    public sealed record LocationStrategyContext
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LocationStrategyContext"/>.
        /// </summary>
        /// <param name="existingGraph">The existing graph value.</param>
        /// <param name="newNodeIds">The identifiers of the new node values.</param>
        /// <param name="attachmentId">The identifier of the attachment.</param>
        /// <param name="random">The random source used to make deterministic choices.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public LocationStrategyContext(LocationGraphSnapshot existingGraph,
            IReadOnlyList<Guid> newNodeIds, Guid? attachmentId, ILocationRandom random)
        {
            ExistingGraph = existingGraph ?? throw new ArgumentNullException(nameof(existingGraph));
            NewNodeIds = Copy(newNodeIds, nameof(newNodeIds));
            AttachmentId = attachmentId;
            Random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public LocationGraphSnapshot ExistingGraph { get; }
        public IReadOnlyList<Guid> NewNodeIds { get; }
        public Guid? AttachmentId { get; }
        public ILocationRandom Random { get; }
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
