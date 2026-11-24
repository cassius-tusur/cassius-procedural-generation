using System;

namespace LocationGenerator.Contracts
{
    /// <summary>
    /// Identifies a relation involved in location topology generation.
    /// </summary>
    /// <param name="SourceId">The identifier of the relation source.</param>
    /// <param name="TargetId">The identifier of the relation target.</param>
    /// <param name="Namespace">The namespace of the relation parameter.</param>
    /// <param name="Name">The name of the relation parameter.</param>
    public sealed record LocationRelationReference(Guid SourceId, Guid TargetId, string Namespace, string Name);
}
