namespace LocationGenerator.Contracts
{
    /// <summary>
    /// Defines the random-number source used by location generation.
    /// </summary>
    public interface ILocationRandom
    {
        /// <summary>
        /// Returns the next random integer below the specified upper bound.
        /// </summary>
        /// <param name="exclusiveMax">The exclusive upper bound.</param>
        /// <returns>The computed value.</returns>
        int Next(int exclusiveMax);
    }
}
