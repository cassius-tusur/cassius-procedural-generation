using System;
using System.Collections.Generic;
using System.Linq;
using LocationGenerator.Strategies;
using Xunit;

namespace LocationGenerator.Tests.Support
{
    internal static class TopologyAssertions
    {
        public static IReadOnlyDictionary<Guid, int> Degrees(
            IReadOnlyList<Guid> nodeIds, IReadOnlyList<LocationPassageProposal> passages)
        {
            var degrees = nodeIds.Distinct().ToDictionary(id => id, _ => 0);
            foreach (var passage in passages)
            {
                Assert.NotEqual(passage.FirstId, passage.SecondId);
                Assert.Contains(passage.FirstId, degrees.Keys);
                Assert.Contains(passage.SecondId, degrees.Keys);
                degrees[passage.FirstId]++;
                degrees[passage.SecondId]++;
            }
            return degrees;
        }

        public static void AssertConnected(IReadOnlyList<Guid> nodeIds,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            Assert.NotEmpty(nodeIds);
            var adjacency = nodeIds.Distinct().ToDictionary(id => id, _ => new HashSet<Guid>());
            foreach (var passage in passages)
            {
                Assert.Contains(passage.FirstId, adjacency.Keys);
                Assert.Contains(passage.SecondId, adjacency.Keys);
                adjacency[passage.FirstId].Add(passage.SecondId);
                adjacency[passage.SecondId].Add(passage.FirstId);
            }

            var visited = new HashSet<Guid>();
            var pending = new Queue<Guid>();
            pending.Enqueue(nodeIds[0]);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!visited.Add(current)) continue;
                foreach (var neighbor in adjacency[current]) pending.Enqueue(neighbor);
            }
            Assert.Equal(adjacency.Count, visited.Count);
        }

        public static int Components(IReadOnlyList<Guid> nodeIds,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            var adjacency = nodeIds.Distinct().ToDictionary(id => id, _ => new HashSet<Guid>());
            foreach (var passage in passages)
            {
                adjacency[passage.FirstId].Add(passage.SecondId);
                adjacency[passage.SecondId].Add(passage.FirstId);
            }
            var visited = new HashSet<Guid>();
            var components = 0;
            foreach (var id in adjacency.Keys)
            {
                if (!visited.Add(id)) continue;
                components++;
                var pending = new Queue<Guid>();
                pending.Enqueue(id);
                while (pending.Count > 0)
                {
                    var current = pending.Dequeue();
                    foreach (var neighbor in adjacency[current])
                        if (visited.Add(neighbor)) pending.Enqueue(neighbor);
                }
            }
            return components;
        }

        public static int CycleRank(IReadOnlyList<Guid> nodeIds,
            IReadOnlyList<LocationPassageProposal> passages)
        {
            return passages.Count - nodeIds.Count + Components(nodeIds, passages);
        }

        public static bool HasEdge(IReadOnlyList<LocationPassageProposal> passages,
            Guid first, Guid second)
        {
            return passages.Any(passage =>
                (passage.FirstId == first && passage.SecondId == second) ||
                (passage.FirstId == second && passage.SecondId == first));
        }
    }
}
