using BaseClasses.Model.Params;

namespace LocationGenerator.Params
{
    /// <summary>
    /// Defines relation keys used by generated location topologies.
    /// </summary>
    public static class LocationRelationKeys
    {
        private const string KeyNamespace = "LocationGenerator";

        public static readonly ParamKey<bool> Entrance = new ParamKey<bool>("Entrance", KeyNamespace);
    }
}
