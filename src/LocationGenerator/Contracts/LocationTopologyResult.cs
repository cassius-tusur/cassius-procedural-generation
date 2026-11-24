using System;
using System.Collections.Generic;
using BaseClasses.Interface;

namespace LocationGenerator.Contracts
{
    /// <summary>
    /// Contains the locations created, removed, and attached by topology generation.
    /// </summary>
    public sealed record LocationTopologyResult
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LocationTopologyResult"/>.
        /// </summary>
        /// <param name="root">The root location for the operation.</param>
        /// <param name="createdLocations">The created locations value.</param>
        /// <param name="attachment">The attachment value.</param>
        /// <param name="removedElementIds">The identifiers of the removed element values.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public LocationTopologyResult(IElement root, IReadOnlyList<IElement> createdLocations,
            IElement? attachment, IReadOnlyList<Guid> removedElementIds)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            CreatedLocations = Copy(createdLocations, nameof(createdLocations));
            Attachment = attachment;
            RemovedElementIds = Copy(removedElementIds, nameof(removedElementIds));
        }

        public IElement Root { get; }
        public IReadOnlyList<IElement> CreatedLocations { get; }
        public IElement? Attachment { get; }
        public IReadOnlyList<Guid> RemovedElementIds { get; }
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
