using System;

namespace LocationGenerator.Contracts
{
    /// <summary>
    /// Specifies how a location topology should be generated.
    /// </summary>
    /// <param name="StrategyId">The identifier of the topology strategy to use.</param>
    /// <param name="NodeCount">The number of locations to generate.</param>
    /// <param name="Options">The options consumed by the selected strategy.</param>
    /// <param name="Mode">The mode that controls how generated locations affect existing topology.</param>
    /// <param name="AttachmentId">The optional existing location to which the new topology is attached.</param>
    /// <seealso cref="ILocationTopologyOptions"/>
    public sealed record LocationTopologyRequest(
        string StrategyId,
        int NodeCount,
        ILocationTopologyOptions Options,
        LocationGenerationMode Mode,
        Guid? AttachmentId = null);
}
