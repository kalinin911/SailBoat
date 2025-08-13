using System;
using UnityEngine;
using Zenject;
using Core.HexGrid;
using Infrastructure.Events;

namespace Core.Input
{
    public class InputHandler : MonoBehaviour, IInputHandler, IInitializable, IDisposable
    {
        public event Action<Vector3> OnMapClicked;
        
        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IGameEventManager _eventManager;
        [Inject] private UnityEngine.Camera _mainCamera;

        public void Initialize()
        {
            // Input initialization if needed
        }

        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                var ray = _mainCamera.ScreenPointToRay(UnityEngine.Input.mousePosition);
                
                if (Physics.Raycast(ray, out var hit))
                {
                    var hexCoord = _hexGridManager.WorldToHex(hit.point);
                    
                    if (_hexGridManager.IsValidHex(hexCoord))
                    {
                        var worldPos = _hexGridManager.HexToWorld(hexCoord);
                        OnMapClicked?.Invoke(worldPos);
                        _eventManager.TriggerHexClicked(hexCoord.ToOffset(), worldPos);
                    }
                }
            }
        }

        public void Dispose()
        {
            OnMapClicked = null;
        }
    }
}