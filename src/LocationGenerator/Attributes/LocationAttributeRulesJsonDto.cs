namespace LocationGenerator.Attributes
{
    // JSON uses entry arrays because the domain lookup tables have composite keys.
    internal sealed class LocationAttributeRulesJsonDto
    {
        public string? Id { get; set; }
        public string[]? Biomes { get; set; }
        public string[]? Sizes { get; set; }
        public string[]? Densities { get; set; }
        public string[]? Preservations { get; set; }
        public DensityEntry?[]? DensityByBiomeAndSize { get; set; }
        public PreservationEntry?[]? PreservationByBiomeAndDensity { get; set; }

        internal sealed class DensityEntry
        {
            public string? Biome { get; set; }
            public string? Size { get; set; }
            public string[]? Values { get; set; }
        }

        internal sealed class PreservationEntry
        {
            public string? Biome { get; set; }
            public string? Density { get; set; }
            public string[]? Values { get; set; }
        }
    }
}
