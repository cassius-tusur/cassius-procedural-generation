using LocationGenerator.Contracts;

namespace LocationGenerator.Strategies
{
    /// <summary>
    /// Defines generation options for a loop topology.
    /// </summary>
    /// <param name="CycleCount">The number of independent cycles to create.</param>
    /// <param name="MaxDegree">The maximum number of passages incident to one location.</param>
    /// <seealso cref="LoopTopologyStrategy"/>
    public sealed record LoopOptions(int CycleCount, int MaxDegree) : ILocationTopologyOptions;
}
