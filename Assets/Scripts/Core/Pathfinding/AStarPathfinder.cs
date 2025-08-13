using System.Collections.Generic;
using System.Linq;
using Core.HexGrid;

namespace Core.Pathfinding
{
    public class AStarPathfinder : IPathfinder
    {
        private readonly IHexGridManager _hexGridManager;

        public AStarPathfinder(IHexGridManager hexGridManager)
        {
            _hexGridManager = hexGridManager;
        }

        public HexCoordinate[] FindPath(HexCoordinate start, HexCoordinate goal)
        {
            if(!_hexGridManager.IsWalkable(start) || !_hexGridManager.IsWalkable(goal))
                return new HexCoordinate[0];

            var openSet = new HashSet<HexCoordinate> { start };
            var cameFrom = new Dictionary<HexCoordinate, HexCoordinate>();
            var gScore = new Dictionary<HexCoordinate, float>{[start] = 0};
            var fScore = new Dictionary<HexCoordinate, float>{[start] = start.DistanceTo(goal)};

            while (openSet.Count > 0)
            {
                var current = openSet.OrderBy(h => fScore.GetValueOrDefault(h, float.MaxValue)).First();

                if (current == goal)
                    return ReconstructPath(cameFrom, current);
                
                openSet.Remove(current);

                foreach (var neighbor in current.GetNeighbors())
                {
                    if(!_hexGridManager.IsWalkable(neighbor))
                        continue;

                    var tentativeGScore = gScore[current] + 1f; // Uniform cost

                    if (tentativeGScore < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + neighbor.DistanceTo(goal);
                        openSet.Add(neighbor);
                    }
                }
            }
            
            return new HexCoordinate[0]; //No path found
        }

        public bool HasValidPath(HexCoordinate start, HexCoordinate goal)
        {
            return FindPath(start, goal).Length > 0;
        }

        private HexCoordinate[] ReconstructPath(Dictionary<HexCoordinate, HexCoordinate> cameFrom,
            HexCoordinate current)
        {
            var path = new List<HexCoordinate>{current};

            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }
            
            path.Reverse();
            return path.ToArray();
        }
    }
}