using System;
using System.Collections.Generic;

namespace LocationGenerator.Tests.Support
{
    /// <summary>
    /// Brute-force feasibility oracle for tiny simple graphs.  This deliberately
    /// enumerates edge subsets instead of sharing construction logic with the
    /// production degree-bounded builder.
    /// </summary>
    internal static class SmallGraphOracle
    {
        public static bool IsFeasible(int nodeCount, int cycleCount, int maxDegree,
            bool reserveFirstNode = false)
        {
            if (nodeCount < 1 || nodeCount > 5 || cycleCount < 0 || maxDegree < 2)
                return false;

            var edgeCount = checked((long)nodeCount - 1L + cycleCount);
            var maximumEdges = checked((long)nodeCount * (nodeCount - 1) / 2L);
            if (edgeCount < 0 || edgeCount > maximumEdges)
                return false;

            var candidates = new List<(int First, int Second)>();
            for (var first = 0; first < nodeCount; first++)
                for (var second = first + 1; second < nodeCount; second++)
                    candidates.Add((first, second));

            var combinations = 1 << candidates.Count;
            for (var mask = 0; mask < combinations; mask++)
            {
                if (CountBits(mask) != edgeCount) continue;
                var degrees = new int[nodeCount];
                var adjacency = new List<int>[nodeCount];
                for (var index = 0; index < nodeCount; index++) adjacency[index] = new List<int>();

                for (var index = 0; index < candidates.Count; index++)
                {
                    if ((mask & (1 << index)) == 0) continue;
                    var edge = candidates[index];
                    degrees[edge.First]++;
                    degrees[edge.Second]++;
                    adjacency[edge.First].Add(edge.Second);
                    adjacency[edge.Second].Add(edge.First);
                }

                if (!IsConnected(adjacency)) continue;
                var feasible = true;
                for (var index = 0; index < nodeCount; index++)
                {
                    var capacity = maxDegree - (reserveFirstNode && index == 0 ? 1 : 0);
                    if (degrees[index] > capacity)
                    {
                        feasible = false;
                        break;
                    }
                }
                if (feasible) return true;
            }

            return false;
        }

        private static bool IsConnected(IReadOnlyList<List<int>> adjacency)
        {
            var seen = new bool[adjacency.Count];
            var pending = new Queue<int>();
            pending.Enqueue(0);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (seen[current]) continue;
                seen[current] = true;
                foreach (var neighbor in adjacency[current])
                    if (!seen[neighbor]) pending.Enqueue(neighbor);
            }
            for (var index = 0; index < seen.Length; index++)
                if (!seen[index]) return false;
            return true;
        }

        private static int CountBits(int value)
        {
            var count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }
    }
}
