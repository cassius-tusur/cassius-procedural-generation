using System;
using System.Collections.Generic;
using System.Linq;
using BaseClasses.Enum;
using BaseClasses.Interface;
using BaseClasses.Model;
using BaseClasses.Services.Binds;
using LocationGenerator.Contracts;
using LocationGenerator.Generation;
using LocationGenerator.Params;
using LocationGenerator.Presets;
using LocationGenerator.Strategies;

namespace LocationGenerator.Example
{
    internal static class Program
    {
        private static void Main()
        {
            var firstTopology = ReadTopology("first", defaultChoice: 4);
            var secondTopology = ReadTopology("second", defaultChoice: 1);
            var output = ReadOutput();

            var plot = new Plot();
            var world = new Element(ElemType.Location, "World");
            plot.Add(world);

            var generator = new LocationTopologyGenerator(
                firstTopology.Request,
                LocationStrategyRegistry.CreateDefault(),
                new ZeroRandom());

            var settlementResult = generator.Generate(
                plot, world, firstTopology.Request);
            NameLocations(settlementResult.CreatedLocations, "1" + firstTopology.Code);
            var chosenLocation = settlementResult.CreatedLocations.Last();
            chosenLocation.Name += " (expanded)";

            var adventureResult = generator.Generate(
                plot, chosenLocation, secondTopology.Request);
            NameLocations(adventureResult.CreatedLocations, "2" + secondTopology.Code);

            Console.WriteLine("LocationGenerator example");
            Console.WriteLine("==========================");
            Console.WriteLine($"First topology: {firstTopology.Name} ({settlementResult.CreatedLocations.Count} locations)");
            Console.WriteLine($"Second topology: {secondTopology.Name} ({adventureResult.CreatedLocations.Count} locations)");
            Console.WriteLine($"Output: {output}");
            Console.WriteLine("Expanded child: " + chosenLocation.Id);
            Console.WriteLine();

            PrintGraph(plot, world, output);
        }

        #region MyRegion

        private static TopologyChoice ReadTopology(string iteration, int defaultChoice)
        {
            while (true)
            {
                Console.WriteLine($"Choose topology for the {iteration} generation (Enter = {defaultChoice}):");
                Console.WriteLine("  1. Linear adventure");
                Console.WriteLine("  2. Branching lair");
                Console.WriteLine("  3. Loop dungeon");
                Console.WriteLine("  4. Settlement hub");
                Console.WriteLine("  5. Region routes");
                Console.Write("Choice: ");
                var input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input))
                    return CreateTopologyChoice(defaultChoice);
                if (int.TryParse(input, out var choice) && choice >= 1 && choice <= 5)
                    return CreateTopologyChoice(choice);

                Console.WriteLine("Please enter a number from 1 to 5.");
                Console.WriteLine();
            }
        }

        private static TopologyChoice CreateTopologyChoice(int choice)
        {
            return choice switch
            {
                1 => new TopologyChoice("L", "Linear adventure", LocationTopologyPresets.LinearAdventure()),
                2 => new TopologyChoice("B", "Branching lair", LocationTopologyPresets.BranchingLair()),
                3 => new TopologyChoice("D", "Loop dungeon", LocationTopologyPresets.LoopDungeon()),
                4 => new TopologyChoice("S", "Settlement hub", LocationTopologyPresets.Settlement()),
                5 => new TopologyChoice("R", "Region routes", LocationTopologyPresets.RegionRoutes()),
                _ => throw new ArgumentOutOfRangeException(nameof(choice))
            };
        }

        private static GraphOutput ReadOutput()
        {
            while (true)
            {
                Console.WriteLine("Choose graph output (Enter = 3):");
                Console.WriteLine("  1. Contains only");
                Console.WriteLine("  2. Passage only");
                Console.WriteLine("  3. Contains and passage");
                Console.Write("Choice: ");
                var input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input) || input == "3")
                    return GraphOutput.Both;
                if (input == "1")
                    return GraphOutput.Contains;
                if (input == "2")
                    return GraphOutput.Passage;

                Console.WriteLine("Please enter a number from 1 to 3.");
                Console.WriteLine();
            }
        }

        private static void NameLocations(IReadOnlyList<IElement> locations, string prefix)
        {
            for (var index = 0; index < locations.Count; index++)
            {
                locations[index].Name = $"{prefix}-{index + 1:00}";
            }
        }

        private static void PrintGraph(Plot plot, IElement root, GraphOutput output)
        {
            const int width = 120;
            const int height = 32;
            var nodes = plot.Elements
                .Where(element => element.Type == ElemType.Location)
                .OrderBy(element => element.Id == root.Id ? 0 : 1)
                .ThenBy(element => element.Name, StringComparer.Ordinal)
                .ToArray();
            var edges = GetEdges(plot, nodes, output);
            var positions = Layout(nodes, edges, width, height);
            var canvas = new ConsoleCanvas(width, height);

            foreach (var edge in edges)
                canvas.DrawLine(positions[edge.Source], positions[edge.Target], edge.Symbol);
            foreach (var element in nodes)
            {
                var position = positions[element.Id];
                canvas.DrawText(position.X, position.Y, $"[{ShortLabel(element, root)}]");
            }

            Console.WriteLine($"Location graph (automatic force-directed layout; {OutputLegend(output)}):");
            Console.WriteLine(canvas.Render());
        }

        private static List<(Guid Source, Guid Target, char Symbol)> GetEdges(
            Plot plot, IReadOnlyList<IElement> nodes, GraphOutput output)
        {
            var nodeIds = new HashSet<Guid>(nodes.Select(element => element.Id));
            var edges = new List<(Guid Source, Guid Target, char Symbol)>();
            var seen = new HashSet<string>();
            foreach (var relation in plot.Relations)
            {
                if (!nodeIds.Contains(relation.Source.Id) || !nodeIds.Contains(relation.Target.Id))
                    continue;

                var isContains = relation.Param.Equals(BaseRelationKeys.Contains);
                var isPassage = relation.Param.Equals(BaseRelationKeys.Passage);
                if ((isContains && output == GraphOutput.Passage) ||
                    (isPassage && output == GraphOutput.Contains) ||
                    (!isContains && !isPassage))
                    continue;

                var symbol = isContains ? '=' : '-';

                var first = relation.Source.Id.CompareTo(relation.Target.Id) < 0
                    ? relation.Source.Id : relation.Target.Id;
                var second = first == relation.Source.Id ? relation.Target.Id : relation.Source.Id;
                var key = $"{first}:{second}:{symbol}";
                if (seen.Add(key))
                    edges.Add((first, second, symbol));
            }

            return edges.OrderBy(edge => edge.Source)
                .ThenBy(edge => edge.Target)
                .ThenBy(edge => edge.Symbol)
                .ToList();
        }

        private static string OutputLegend(GraphOutput output)
        {
            return output switch
            {
                GraphOutput.Contains => "= contains",
                GraphOutput.Passage => "- passage",
                _ => "= contains, - passage"
            };
        }

        private static Dictionary<Guid, (int X, int Y)> Layout(
            IReadOnlyList<IElement> nodes,
            IReadOnlyList<(Guid Source, Guid Target, char Symbol)> edges,
            int width, int height)
        {
            var indexById = nodes.Select((element, index) => new { element.Id, index })
                .ToDictionary(item => item.Id, item => item.index);
            var x = new double[nodes.Count];
            var y = new double[nodes.Count];
            var centerX = width / 2.0;
            var centerY = height / 2.0;
            var radius = Math.Min(width, height) * 0.35;

            for (var index = 0; index < nodes.Count; index++)
            {
                var angle = 2 * Math.PI * index / Math.Max(nodes.Count, 1);
                x[index] = centerX + Math.Cos(angle) * radius;
                y[index] = centerY + Math.Sin(angle) * radius;
            }

            const int iterations = 240;
            var idealDistance = Math.Sqrt(width * height / (double)Math.Max(nodes.Count, 1));
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var forceX = new double[nodes.Count];
                var forceY = new double[nodes.Count];

                for (var first = 0; first < nodes.Count; first++)
                for (var second = first + 1; second < nodes.Count; second++)
                {
                    var deltaX = x[first] - x[second];
                    var deltaY = y[first] - y[second];
                    var distance = Math.Max(Math.Sqrt(deltaX * deltaX + deltaY * deltaY), 0.1);
                    var force = idealDistance * idealDistance / distance;
                    forceX[first] += deltaX / distance * force;
                    forceY[first] += deltaY / distance * force;
                    forceX[second] -= deltaX / distance * force;
                    forceY[second] -= deltaY / distance * force;
                }

                foreach (var edge in edges)
                {
                    var source = indexById[edge.Source];
                    var target = indexById[edge.Target];
                    var deltaX = x[source] - x[target];
                    var deltaY = y[source] - y[target];
                    var distance = Math.Max(Math.Sqrt(deltaX * deltaX + deltaY * deltaY), 0.1);
                    var force = distance * distance / idealDistance;
                    forceX[source] -= deltaX / distance * force;
                    forceY[source] -= deltaY / distance * force;
                    forceX[target] += deltaX / distance * force;
                    forceY[target] += deltaY / distance * force;
                }

                var temperature = 7.0 * (1.0 - iteration / (double)iterations);
                for (var index = 0; index < nodes.Count; index++)
                {
                    var forceLength = Math.Max(Math.Sqrt(forceX[index] * forceX[index] +
                        forceY[index] * forceY[index]), 0.1);
                    var step = Math.Min(forceLength, temperature);
                    x[index] = Math.Clamp(x[index] + forceX[index] / forceLength * step, 4, width - 5);
                    y[index] = Math.Clamp(y[index] + forceY[index] / forceLength * step, 1, height - 2);
                }
            }

            var positions = new Dictionary<Guid, (int X, int Y)>();
            for (var index = 0; index < nodes.Count; index++)
                positions[nodes[index].Id] = ((int)Math.Round(x[index]), (int)Math.Round(y[index]));
            return positions;
        }

        private static string ShortLabel(IElement element, IElement root)
        {
            if (element.Id == root.Id)
                return "ROOT";
            var separator = element.Name.IndexOf('-');
            if (separator > 0 && element.Name.Length > separator + 2)
                return element.Name.Substring(0, separator) +
                       element.Name.Substring(separator + 1, 2) +
                       (element.Name.Contains("expanded", StringComparison.Ordinal) ? "*" : "");
            return element.Name;
        }

        private enum GraphOutput
        {
            Contains,
            Passage,
            Both
        }

        private sealed record TopologyChoice(
            string Code,
            string Name,
            LocationTopologyRequest Request);

        private sealed class ConsoleCanvas
        {
            private readonly char[,] _cells;

            public ConsoleCanvas(int width, int height)
            {
                _cells = new char[height, width];
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    _cells[y, x] = ' ';
            }

            public void DrawLine((int X, int Y) source, (int X, int Y) target, char edge)
            {
                var x = source.X;
                var y = source.Y;
                var deltaX = Math.Abs(target.X - source.X);
                var stepX = source.X < target.X ? 1 : -1;
                var deltaY = -Math.Abs(target.Y - source.Y);
                var stepY = source.Y < target.Y ? 1 : -1;
                var error = deltaX + deltaY;

                while (true)
                {
                    Put(x, y, edge);
                    if (x == target.X && y == target.Y)
                        break;

                    var doubleError = 2 * error;
                    if (doubleError >= deltaY)
                    {
                        error += deltaY;
                        x += stepX;
                    }
                    if (doubleError <= deltaX)
                    {
                        error += deltaX;
                        y += stepY;
                    }
                }
            }

            public void DrawText(int centerX, int y, string text)
            {
                var startX = centerX - text.Length / 2;
                for (var index = 0; index < text.Length; index++)
                {
                    if (y >= 0 && y < _cells.GetLength(0) &&
                        startX + index >= 0 && startX + index < _cells.GetLength(1))
                        _cells[y, startX + index] = text[index];
                }
            }

            public string Render()
            {
                var lines = new string[_cells.GetLength(0)];
                for (var y = 0; y < _cells.GetLength(0); y++)
                {
                    var line = new char[_cells.GetLength(1)];
                    for (var x = 0; x < _cells.GetLength(1); x++)
                        line[x] = _cells[y, x];
                    lines[y] = new string(line).TrimEnd();
                }

                return string.Join(Environment.NewLine, lines).TrimEnd();
            }

            private void Put(int x, int y, char value)
            {
                if (x < 0 || x >= _cells.GetLength(1) || y < 0 || y >= _cells.GetLength(0))
                    return;

                var current = _cells[y, x];
                _cells[y, x] = current == ' ' || current == value ? value : '+';
            }
        }

        private sealed class ZeroRandom : ILocationRandom
        {
            public int Next(int exclusiveMax)
            {
                if (exclusiveMax <= 0)
                    throw new ArgumentOutOfRangeException(nameof(exclusiveMax));

                return 0;
            }
        }

        #endregion
    }
}
