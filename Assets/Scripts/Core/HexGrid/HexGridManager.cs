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
            
            if (_positionLookup.TryGetValue(gridPos, out var cachedHex))
                return cachedHex;
            
            return FindClosestHex(worldPosition);
        }

        private HexCoordinate FindClosestHex(Vector3 worldPosition)
        {
            if (_hexTiles.Count == 0)
                return new HexCoordinate(0, 0);

            var closestHex = new HexCoordinate(0, 0);
            var closestDistanceSqr = float.MaxValue;
            
            foreach (var kvp in _hexTiles)
            {
                var tilePosition = kvp.Value.transform.position;
                var distanceSqr = (worldPosition - tilePosition).sqrMagnitude;
                
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
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
            if (tile == null)
            {
                Debug.LogError($"Cannot register null tile at coordinate {coordinate}");
                return;
            }

            _hexTiles[coordinate] = tile;
            
            var worldPos = tile.transform.position;
            var gridPos = new Vector3Int(
                Mathf.RoundToInt(worldPos.x),
                Mathf.RoundToInt(worldPos.y),
                Mathf.RoundToInt(worldPos.z)
            );
            _positionLookup[gridPos] = coordinate;
        }

        public void UnregisterHexTile(HexCoordinate coordinate)
        {
            if (_hexTiles.TryGetValue(coordinate, out var tile))
            {
                var worldPos = tile.transform.position;
                var gridPos = new Vector3Int(
                    Mathf.RoundToInt(worldPos.x),
                    Mathf.RoundToInt(worldPos.y),
                    Mathf.RoundToInt(worldPos.z)
                );
                
                _positionLookup.Remove(gridPos);
                _hexTiles.Remove(coordinate);
            }
        }

        public void Clear()
        {
            _hexTiles.Clear();
            _positionLookup.Clear();
        }

        public int GetTileCount()
        {
            return _hexTiles.Count;
        }

        public IEnumerable<HexCoordinate> GetAllCoordinates()
        {
            return _hexTiles.Keys;
        }

        public IEnumerable<HexTile> GetAllTiles()
        {
            return _hexTiles.Values;
        }
    }
}