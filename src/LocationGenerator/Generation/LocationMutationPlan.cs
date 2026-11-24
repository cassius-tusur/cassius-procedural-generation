using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BaseClasses.Interface;
using BaseClasses.Model;

[assembly: InternalsVisibleTo("LocationGenerator.Tests")]

namespace LocationGenerator.Generation
{
    /// <summary>
    /// Collects detached location and relation mutations before validation and commit.
    /// </summary>
    internal sealed class LocationMutationPlan
    {
        private readonly List<IElement> _newLocations = new List<IElement>();
        private readonly List<IElement> _removedLocations = new List<IElement>();
        private readonly List<Relation> _removedRelations = new List<Relation>();
        private readonly List<Relation> _relations = new List<Relation>();

        public IReadOnlyList<IElement> NewLocations => _newLocations;
        public IReadOnlyList<IElement> RemovedLocations => _removedLocations;
        public IReadOnlyList<Relation> RemovedRelations => _removedRelations;
        public IReadOnlyList<Relation> Relations => _relations;

        /// <summary>
        /// Adds location.
        /// </summary>
        /// <param name="location">The location to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void AddLocation(IElement location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            _newLocations.Add(location);
        }

        /// <summary>
        /// Removes location.
        /// </summary>
        /// <param name="location">The location to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void RemoveLocation(IElement location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));
            _removedLocations.Add(location);
        }

        /// <summary>
        /// Removes relation.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void RemoveRelation(Relation relation)
        {
            if (relation == null) throw new ArgumentNullException(nameof(relation));
            _removedRelations.Add(relation);
        }

        /// <summary>
        /// Removes scope.
        /// </summary>
        /// <param name="scope">The scope value.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void RemoveScope(LocationGenerationScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            foreach (var relation in scope.RemovedRelations) RemoveRelation(relation);
            foreach (var location in scope.RemovedLocations) RemoveLocation(location);
        }

        /// <summary>
        /// Adds relation.
        /// </summary>
        /// <param name="relation">The relation to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void AddRelation(Relation relation)
        {
            if (relation == null) throw new ArgumentNullException(nameof(relation));
            _relations.Add(relation);
        }

        /// <summary>
        /// Adds passage.
        /// </summary>
        /// <param name="first">The first value.</param>
        /// <param name="second">The second value.</param>
        /// <param name="passageKey">The passage key value.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public void AddPassage(IElement first, IElement second, IParamKey passageKey)
        {
            if (first == null) throw new ArgumentNullException(nameof(first));
            if (second == null) throw new ArgumentNullException(nameof(second));
            if (passageKey == null) throw new ArgumentNullException(nameof(passageKey));

            _relations.Add(new Relation(first, second, passageKey, true));
            _relations.Add(new Relation(second, first, passageKey, true));
        }
    }

}
