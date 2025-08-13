using System.Collections.Generic;
using UnityEngine;

namespace Core.HexGrid
{
    public class HexGridManager : IHexGridManager
    {
        private readonly Dictionary<HexCoordinate, HexTile> _hexTiles = new();
        private float _hexSize = 1f;
        
        public HexCoordinate WorldToHex(Vector3 worldPosition)
        {
            return HexCoordinate.FromWorldPosition(worldPosition, _hexSize);
        }

        public Vector3 HexToWorld(HexCoordinate hexCoordinate)
        {
            return hexCoordinate.ToWorldPosition(_hexSize);
        }

        public bool IsValidHex(HexCoordinate hex)
        {
            return _hexTiles.ContainsKey(hex);
        }

        public bool IsWalkable(HexCoordinate hex)
        {
            return _hexTiles.TryGetValue(hex, out var tile) && tile.IsWalkable;
        }

        public float GetHexSize()
        {
            return _hexSize;
        }

        public void SetHexSize(float size)
        {
            _hexSize = size;
        }

        public HexTile GetHexTile(HexCoordinate coordinate)
        {
            _hexTiles.TryGetValue(coordinate, out var tile);
            return tile;
        }

        public void RegisterHexTile(HexCoordinate coordinate, HexTile tile)
        {
            _hexTiles[coordinate] = tile;
        }
    }
}