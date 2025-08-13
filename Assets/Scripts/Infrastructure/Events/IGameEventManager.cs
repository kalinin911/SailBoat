using UnityEngine;
using System;
using Core.HexGrid;


public interface IGameEventManager
{
    event Action<Vector2Int, Vector3> OnHexClicked;
    event Action<HexCoordinate[]> OnPathCalculated;
    event Action<Vector3[]> OnBoatMovementStarted;
    event Action<Vector3> OnBoatMovementCompleted;

    void TriggerHexClicked(Vector2Int hexCoordinate, Vector3 worldPosition);
    void TriggerPathCalculated(HexCoordinate[] path);
    void TriggerBoatMovementStarted(Vector3[] path);
    void TriggerBoatMovementCompleted(Vector3 finalPosition);

}
