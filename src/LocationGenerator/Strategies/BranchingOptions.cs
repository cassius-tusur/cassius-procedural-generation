using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Defines generation options for a branching topology.
    /// </summary>
    /// <param name="MaxDegree">The maximum number of passages incident to one location.</param>
    /// <seealso cref="BranchingTopologyStrategy"/>
    public sealed record BranchingOptions(int MaxDegree) : ILocationTopologyOptions;
}
