using System.Collections.Generic;
using LocationGenerator.Contracts;

namespace LocationGenerator.Tests.Support
{
    internal sealed class SequenceRandom : ILocationRandom
    {
        private readonly Queue<int> _values;

        public SequenceRandom(params int[] values)
        {
            _values = new Queue<int>(values ?? new int[0]);
        }

        public int Next(int exclusiveMax)
        {
            return _values.Count == 0 ? 0 : _values.Dequeue();
        }
    }
}
