using BaseClasses.Model.Params;

namespace LocationGenerator.Params
{
    /// <summary>
    /// Defines parameter keys for generated location attributes.
    /// </summary>
    public static class LocationParams
    {
        private const string KeyNamespace = "LocationGenerator";

        public static readonly ParamKey<string> Biome = new ParamKey<string>("Biome", KeyNamespace);
        public static readonly ParamKey<string> Size = new ParamKey<string>("Size", KeyNamespace);
        public static readonly ParamKey<string> Density = new ParamKey<string>("Density", KeyNamespace);
        public static readonly ParamKey<string> Preservation = new ParamKey<string>("Preservation", KeyNamespace);
    }
}
