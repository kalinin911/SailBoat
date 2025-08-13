using Data;
using UnityEngine;

namespace Core.HexGrid
{
    public class HexTile : MonoBehaviour
    {
        [SerializeField] private TileType _tileType;
        [SerializeField] private bool _hasObstacle;
        
        public HexCoordinate Coordinate { get; private set; }
        public TileType TileType => _tileType;
        public bool HasObstacle => _hasObstacle;
        public bool IsWalkable => _tileType == TileType.Water && !_hasObstacle;

        public void Initialize(HexCoordinate coordinate, TileType tileType, bool hasObstacle = false)
        {
            Coordinate = coordinate;
            _tileType = tileType;
            _hasObstacle = hasObstacle;
        }

        public void SetObstacle(bool hasObstacle)
        {
            _hasObstacle = hasObstacle;
        }

        public void SetTileType(TileType tileType)
        {
            _tileType = tileType;
        }
    }
}