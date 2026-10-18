using System;
using System.Collections.Generic;

namespace Eirora.Simulation.Map
{
    /// <summary>What a step between two neighbouring hexes costs, for roads on land and sea routes alike.</summary>
    public interface IStepCost
    {
        /// <summary>
        /// The least any possible step costs, at least 1. The search uses it to estimate the rest of the way, so a step
        /// that costs less than this can make it miss the cheapest path.
        /// </summary>
        int MinimumCost { get; }

        /// <summary>The cost of stepping from <paramref name="origin"/> to its <paramref name="neighbour"/>, or <see cref="PathFinder.Impassable"/>.</summary>
        int Cost(Hex origin, Hex neighbour);
    }

    /// <summary>
    /// Finds the cheapest path between two hexes of a disc with A* (TASK-028). Equally cheap paths are told apart by
    /// a fixed order, so the client and the server always choose the same one (NFR-02): the open hex with the lowest
    /// estimated total is expanded first, then the one nearest the goal, then the first in map order; neighbours are
    /// visited counter-clockwise from east, and a path is only replaced by a strictly cheaper one.
    /// </summary>
    /// <remarks>
    /// All memory is allocated with the finder; a search allocates nothing, so a finder is reused for every search
    /// on the same map. It is not thread-safe.
    /// </remarks>
    public sealed class PathFinder
    {
        public const int Impassable = -1;

        private readonly HexDisc _disc;
        private readonly int[] _cost;
        private readonly int[] _estimate;
        private readonly int[] _parent;
        private readonly int[] _seen;
        private readonly int[] _closed;
        private readonly int[] _heap;
        private readonly int[] _heapPosition;
        private int _heapCount;
        private int _search;

        public PathFinder(HexDisc disc)
        {
            _disc = disc ?? throw new ArgumentNullException(nameof(disc));
            int count = disc.Count;
            _cost = new int[count];
            _estimate = new int[count];
            _parent = new int[count];
            _seen = new int[count];
            _closed = new int[count];
            _heap = new int[count];
            _heapPosition = new int[count];
        }

        /// <summary>
        /// The cheapest path from <paramref name="start"/> to <paramref name="goal"/>, both included, into
        /// <paramref name="path"/>, or false when there is none.
        /// </summary>
        public bool TryFind<TCost>(Hex start, Hex goal, TCost costs, List<Hex> path, out int totalCost)
            where TCost : IStepCost
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            int minimum = costs.MinimumCost;
            if (minimum < 1)
            {
                throw new ArgumentException("The minimum step cost is at least 1.", nameof(costs));
            }

            path.Clear();
            totalCost = 0;
            int from = _disc.IndexOf(start), to = _disc.IndexOf(goal);
            if (from < 0 || to < 0)
            {
                return false;
            }

            BeginSearch();
            Open(from, parent: -1, cost: 0, estimate: start.DistanceTo(goal) * minimum);
            while (_heapCount > 0)
            {
                int current = Pop();
                if (current == to)
                {
                    totalCost = _cost[to];
                    Trace(to, path);
                    return true;
                }

                _closed[current] = _search;
                var hex = _disc[current];
                for (int direction = 0; direction < Hex.Directions; direction++)
                {
                    var next = hex.Neighbour(direction);
                    int index = _disc.IndexOf(next);
                    if (index < 0 || _closed[index] == _search)
                    {
                        continue;
                    }

                    int step = costs.Cost(hex, next);
                    if (step == Impassable)
                    {
                        continue;
                    }

                    if (step < minimum)
                    {
                        throw new InvalidOperationException($"A step from {hex} to {next} costs {step}, less than the minimum {minimum}.");
                    }

                    int cost = _cost[current] + step;
                    if (_seen[index] != _search)
                    {
                        Open(index, current, cost, next.DistanceTo(goal) * minimum);
                    }
                    else if (cost < _cost[index])
                    {
                        _cost[index] = cost;
                        _parent[index] = current;
                        SiftUp(_heapPosition[index]);
                    }
                }
            }

            return false;
        }

        private void BeginSearch()
        {
            _heapCount = 0;
            if (_search == int.MaxValue)
            {
                Array.Clear(_seen, 0, _seen.Length);
                Array.Clear(_closed, 0, _closed.Length);
                _search = 0;
            }

            _search++;
        }

        private void Open(int index, int parent, int cost, int estimate)
        {
            _seen[index] = _search;
            _cost[index] = cost;
            _estimate[index] = estimate;
            _parent[index] = parent;
            _heap[_heapCount] = index;
            _heapPosition[index] = _heapCount;
            _heapCount++;
            SiftUp(_heapCount - 1);
        }

        private int Pop()
        {
            int top = _heap[0];
            _heapCount--;
            if (_heapCount > 0)
            {
                Place(_heap[_heapCount], 0);
                SiftDown(0);
            }

            return top;
        }

        private void Trace(int goal, List<Hex> path)
        {
            for (int index = goal; index >= 0; index = _parent[index])
            {
                path.Add(_disc[index]);
            }

            path.Reverse();
        }

        /// <summary>Whether hex a is expanded before hex b: lower total, then nearer the goal, then map order.</summary>
        private bool Before(int a, int b)
        {
            int totalA = _cost[a] + _estimate[a], totalB = _cost[b] + _estimate[b];
            if (totalA != totalB)
            {
                return totalA < totalB;
            }

            return _estimate[a] != _estimate[b] ? _estimate[a] < _estimate[b] : a < b;
        }

        private void SiftUp(int position)
        {
            int index = _heap[position];
            while (position > 0)
            {
                int parent = (position - 1) / 2;
                if (!Before(index, _heap[parent]))
                {
                    break;
                }

                Place(_heap[parent], position);
                position = parent;
            }

            Place(index, position);
        }

        private void SiftDown(int position)
        {
            int index = _heap[position];
            while (true)
            {
                int child = (2 * position) + 1;
                if (child >= _heapCount)
                {
                    break;
                }

                if (child + 1 < _heapCount && Before(_heap[child + 1], _heap[child]))
                {
                    child++;
                }

                if (!Before(_heap[child], index))
                {
                    break;
                }

                Place(_heap[child], position);
                position = child;
            }

            Place(index, position);
        }

        private void Place(int index, int position)
        {
            _heap[position] = index;
            _heapPosition[index] = position;
        }
    }
}
