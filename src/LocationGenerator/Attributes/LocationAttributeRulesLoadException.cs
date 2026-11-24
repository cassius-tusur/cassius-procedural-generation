using System;

namespace LocationGenerator.Attributes
{
    /// <summary>A rules loading failure, retaining its source and original cause.</summary>
    public sealed class LocationAttributeRulesLoadException : Exception
    {
        /// <summary>The supplied file path, or null when loading a JSON string.</summary>
        public string? SourcePath { get; }

        public LocationAttributeRulesLoadException(string message, string? sourcePath, Exception innerException)
            : base(message, innerException)
        {
            SourcePath = sourcePath;
        }
    }
}
