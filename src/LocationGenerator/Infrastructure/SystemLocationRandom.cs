using System;
using LocationGenerator.Contracts;

namespace LocationGenerator.Infrastructure
{
    /// <summary>
    /// Adapts <see cref="Random"/> to the <see cref="ILocationRandom"/> contract.
    /// </summary>
    internal sealed class SystemLocationRandom : ILocationRandom
    {
        private readonly Random _random = new Random();

        /// <summary>
        /// Returns the next pseudo-random value in the requested range.
        /// </summary>
        /// <param name="exclusiveMax">The exclusive upper bound.</param>
        /// <returns>The computed value.</returns>
        public int Next(int exclusiveMax)
        {
            return _random.Next(exclusiveMax);
        }
    }
}
