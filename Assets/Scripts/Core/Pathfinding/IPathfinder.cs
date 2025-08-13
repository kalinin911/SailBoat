using Core.HexGrid;

namespace Core.Pathfinding
{
    public interface IPathfinder
    {
        HexCoordinate[] FindPath(HexCoordinate start, HexCoordinate goal);
        bool HasValidPath(HexCoordinate start, HexCoordinate goal);
    }
}