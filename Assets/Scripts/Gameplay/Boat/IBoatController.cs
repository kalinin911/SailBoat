using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.HexGrid;

namespace Gameplay.Boat
{
    public interface IBoatController
    {
        Transform Transform { get; }
        HexCoordinate CurrentHex { get; }
        bool IsMoving { get; }
        UniTask MoveToAsync(HexCoordinate[] path);
        void SetPosition(HexCoordinate hex);
        void CancelCurrentMovement();
        HexCoordinate GetCurrentHexFromPosition();
        bool HasValidPosition();
        void UpdateCurrentHex(HexCoordinate hex);
    }
}