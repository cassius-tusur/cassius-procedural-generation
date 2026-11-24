using System;
using System.Collections.Generic;

namespace LocationGenerator.Contracts
{
    /// <summary>
    /// Represents one or more location-generation validation failures.
    /// </summary>
    public sealed class LocationGenerationValidationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LocationGenerationValidationException"/>.
        /// </summary>
        /// <param name="diagnostics">The validation diagnostics to inspect.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public LocationGenerationValidationException(IReadOnlyList<LocationGenerationDiagnostic> diagnostics)
            : base("Location generation validation failed.")
        {
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        public IReadOnlyList<LocationGenerationDiagnostic> Diagnostics { get; }
    }
}
