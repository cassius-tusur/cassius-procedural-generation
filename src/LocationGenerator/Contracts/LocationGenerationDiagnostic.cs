using System;
using System.Collections.Generic;

namespace LocationGenerator.Contracts
{
    /// <summary>
    /// Describes one validation failure produced during location generation.
    /// </summary>
    public sealed record LocationGenerationDiagnostic
    {
        /// <summary>
        /// Initializes a new instance of <see cref="LocationGenerationDiagnostic"/>.
        /// </summary>
        /// <param name="code">The code value.</param>
        /// <param name="path">The path value.</param>
        /// <param name="message">The message value.</param>
        /// <param name="elementIds">The identifiers of the element values.</param>
        /// <param name="relations">The relations to process.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <see langword="null"/>.</exception>
        public LocationGenerationDiagnostic(string code, string path, string message,
            IReadOnlyList<Guid>? elementIds = null,
            IReadOnlyList<LocationRelationReference>? relations = null)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ElementIds = Copy(elementIds);
            Relations = Copy(relations);
        }

        public string Code { get; }
        public string Path { get; }
        public string Message { get; }
        public IReadOnlyList<Guid> ElementIds { get; }
        public IReadOnlyList<LocationRelationReference> Relations { get; }
        /// <summary>
        /// Creates a defensive copy of the supplied collection.
        /// </summary>
        /// <typeparam name="T">The type of value processed by the method.</typeparam>
        /// <param name="values">The values to process.</param>
        /// <returns>The resulting collection.</returns>

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T>? values)
        {
            return Array.AsReadOnly(values == null ? Array.Empty<T>() : new List<T>(values).ToArray());
        }
    }
}
