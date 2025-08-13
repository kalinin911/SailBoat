using UnityEngine;
using Core.HexGrid;

namespace Gameplay.UI
{
    public interface IPathRenderer
    {
        void ShowPath(HexCoordinate[] path);
        void HidePath();
        void SetPathMaterial(Material material);
    }
}