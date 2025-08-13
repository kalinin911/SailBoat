using UnityEngine;

namespace Core.HexGrid
{
    public interface IHexGridManager
    {
        HexCoordinate WorldToHex(Vector3 worldPosition);
        Vector3 HexToWorld(HexCoordinate hexCoordinate);
        bool IsValidHex(HexCoordinate hex);
        bool IsWalkable(HexCoordinate hex);
        float GetHexSize();
        void SetHexSize(float size);
        HexTile GetHexTile(HexCoordinate coordinate);
        void RegisterHexTile(HexCoordinate coordinate, HexTile tile);
    }
}