using System;
using System.IO;
using LocationGenerator.Attributes;

namespace LocationGenerator.Tests.Support
{
    internal static class LocationAttributeRulesTestFixture
    {
        public static LocationAttributeRules LoadDungeonFlatV1() =>
            LocationAttributeRulesLoader.LoadFile(Path.Combine(
                AppContext.BaseDirectory, "Content", "LocationAttributes", "DungeonFlatV1.json"));
    }
}
