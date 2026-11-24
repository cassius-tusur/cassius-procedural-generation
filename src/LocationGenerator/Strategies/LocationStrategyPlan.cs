using System;
using System.Collections.Generic;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Contains passage proposals and the attachment selected by a topology strategy.
    /// </summary>
    public sealed record LocationStrategyPlan
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LocationStrategyPlan"/>.
        /// </summary>
        /// <param name="passages">The passages value.</param>
        /// <param name="attachmentId">The identifier of the attachment.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public LocationStrategyPlan(IReadOnlyList<LocationPassageProposal> passages, Guid? attachmentId)
        {
            if (passages == null) throw new ArgumentNullException(nameof(passages));
            Passages = Array.AsReadOnly(new List<LocationPassageProposal>(passages).ToArray());
            AttachmentId = attachmentId;
        }

        public IReadOnlyList<LocationPassageProposal> Passages { get; }
        public Guid? AttachmentId { get; }
    }
}
