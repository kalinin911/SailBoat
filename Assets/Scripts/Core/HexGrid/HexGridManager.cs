using System.Collections.Generic;
using UnityEngine;

namespace Core.HexGrid
{
    public class HexGridManager : IHexGridManager
    {
        private readonly Dictionary<HexCoordinate, HexTile> _hexTiles = new();
        private readonly Dictionary<Vector3Int, HexCoordinate> _positionLookup = new();
        private float _hexSize = 0.6f;
        
        public HexCoordinate WorldToHex(Vector3 worldPosition)
        {
            var gridPos = new Vector3Int(
                Mathf.RoundToInt(worldPosition.x),
                Mathf.RoundToInt(worldPosition.y),
                Mathf.RoundToInt(worldPosition.z)
            );
            
            if (_positionLookup.TryGetValue(gridPos, out var hex))
                return hex;
            
            var closestHex = new HexCoordinate(0, 0);
            var closestDistance = float.MaxValue;
            
            foreach (var kvp in _hexTiles)
            {
                var distance = Vector3.Distance(worldPosition, kvp.Value.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestHex = kvp.Key;
                }
            }
            
            return closestHex;
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
            
            var worldPos = tile.transform.position;
            var gridPos = new Vector3Int(
                Mathf.RoundToInt(worldPos.x),
                Mathf.RoundToInt(worldPos.y),
                Mathf.RoundToInt(worldPos.z)
            );
            _positionLookup[gridPos] = coordinate;
        }
    }
}