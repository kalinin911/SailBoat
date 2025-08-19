using System;
using Core.HexGrid;
using UnityEngine;

namespace Infrastructure.Events
{
    public class GameEventManager : IGameEventManager
    {
        public event Action<Vector2Int, Vector3> OnHexClicked;
        public event Action<HexCoordinate[]> OnPathCalculated;
        public event Action<Vector3[]> OnBoatMovementStarted;
        public event Action<Vector3> OnBoatMovementCompleted;
        
        public void TriggerHexClicked(Vector2Int hexCoordinate, Vector3 worldPosition)
        {
            Debug.Log($"GameEventManager.TriggerHexClicked called: {hexCoordinate}, subscribers: {OnHexClicked?.GetInvocationList()?.Length ?? 0}");
            OnHexClicked?.Invoke(hexCoordinate, worldPosition);
        }

        public void TriggerPathCalculated(HexCoordinate[] path)
        {
            OnPathCalculated?.Invoke(path);
        }

        public void TriggerBoatMovementStarted(Vector3[] path)
        {
            OnBoatMovementStarted?.Invoke(path);
        }

        public void TriggerBoatMovementCompleted(Vector3 finalPosition)
        {
            OnBoatMovementCompleted?.Invoke(finalPosition);
        }
    }
}