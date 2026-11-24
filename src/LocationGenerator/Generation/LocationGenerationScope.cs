using System;
using System.Collections.Generic;
using BaseClasses.Interface;
using BaseClasses.Model;
using LocationGenerator.Strategies;

namespace LocationGenerator.Generation
{
    /// <summary>
    /// Describes the existing and removable graph state affected by one generation request.
    /// </summary>
    internal sealed class LocationGenerationScope
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LocationGenerationScope"/>.
        /// </summary>
        /// <param name="root">The root location for the operation.</param>
        /// <param name="existingGraph">The existing graph value.</param>
        /// <param name="existingLocations">The existing locations value.</param>
        /// <param name="removedLocations">The removed locations value.</param>
        /// <param name="removedRelations">The removed relations value.</param>
        public LocationGenerationScope(IElement root, LocationGraphSnapshot existingGraph,
            IReadOnlyDictionary<Guid, IElement> existingLocations,
            IReadOnlyList<IElement> removedLocations, IReadOnlyList<Relation> removedRelations)
        {
            Root = root;
            ExistingGraph = existingGraph;
            ExistingLocations = existingLocations;
            RemovedLocations = removedLocations;
            RemovedRelations = removedRelations;
        }

        public IElement Root { get; }
        public LocationGraphSnapshot ExistingGraph { get; }
        public IReadOnlyDictionary<Guid, IElement> ExistingLocations { get; }
        public IReadOnlyList<IElement> RemovedLocations { get; }
        public IReadOnlyList<Relation> RemovedRelations { get; }
    }
}
