using System;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Represents a proposed undirected passage between two locations.
    /// </summary>
    /// <param name="FirstId">The identifier of the first endpoint.</param>
    /// <param name="SecondId">The identifier of the second endpoint.</param>
    public sealed record LocationPassageProposal(Guid FirstId, Guid SecondId);
}
