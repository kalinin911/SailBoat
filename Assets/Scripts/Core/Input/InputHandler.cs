using System;
using UnityEngine;
using Zenject;
using Core.HexGrid;
using Infrastructure.Events;

namespace Core.Input
{
    public class InputHandler : MonoBehaviour, IInputHandler, IInitializable, IDisposable
    {
        [Header("Input Settings")]
        [SerializeField] private bool _enableMobileInput = true;
        [SerializeField] private float _touchSensitivity = 1f;
        [SerializeField] private float _doubleTapTime = 0.3f;
        [SerializeField] private float _dragThreshold = 50f;

        [Header("Debug")]
        [SerializeField] private bool _debugInput = false;

        public event Action<Vector3> OnMapClicked;
        public event Action<Vector3> OnMapDoubleClicked;
        public event Action<Vector3, Vector3> OnMapDragged;

        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IGameEventManager _eventManager;
        [Inject] private UnityEngine.Camera _mainCamera;

        private float _lastTapTime;
        private Vector3 _lastTapPosition;
        private bool _isDragging;
        private Vector3 _dragStartPosition;
        private Vector2 _lastTouchPosition;

        public void Initialize()
        {
            if (Application.isMobilePlatform)
            {
                _enableMobileInput = true;
                UnityEngine.Input.multiTouchEnabled = false;
            }

            if (_debugInput)
            {
                Debug.Log($"Input Handler initialized - Mobile: {_enableMobileInput}");
            }
        }

        private void Update()
        {
            if (_enableMobileInput && Application.isMobilePlatform)
            {
                HandleMobileInput();
            }
            else
            {
                HandleDesktopInput();
            }
        }

        private void HandleDesktopInput()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                ProcessClick(UnityEngine.Input.mousePosition);
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                ProcessAlternativeClick(UnityEngine.Input.mousePosition);
            }

            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                _isDragging = false;
                _dragStartPosition = UnityEngine.Input.mousePosition;
            }
            else if (UnityEngine.Input.GetMouseButton(0))
            {
                var currentPos = UnityEngine.Input.mousePosition;
                var dragDistance = Vector3.Distance(_dragStartPosition, currentPos);
                
                if (dragDistance > _dragThreshold && !_isDragging)
                {
                    _isDragging = true;
                }
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0) && _isDragging)
            {
                ProcessDrag(_dragStartPosition, UnityEngine.Input.mousePosition);
                _isDragging = false;
            }
        }

        private void HandleMobileInput()
        {
            if (UnityEngine.Input.touchCount == 0)
                return;

            var touch = UnityEngine.Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    HandleTouchBegan(touch);
                    break;
                
                case TouchPhase.Moved:
                    HandleTouchMoved(touch);
                    break;
                
                case TouchPhase.Ended:
                    HandleTouchEnded(touch);
                    break;
                
                case TouchPhase.Canceled:
                    _isDragging = false;
                    break;
            }
        }

        private void HandleTouchBegan(Touch touch)
        {
            _lastTouchPosition = touch.position;
            _dragStartPosition = touch.position;
            _isDragging = false;

            float timeSinceLastTap = Time.time - _lastTapTime;
            float distanceFromLastTap = Vector2.Distance(touch.position, _lastTapPosition);

            if (timeSinceLastTap < _doubleTapTime && distanceFromLastTap < _dragThreshold)
            {
                ProcessDoubleClick(touch.position);
            }

            _lastTapTime = Time.time;
            _lastTapPosition = touch.position;
        }

        private void HandleTouchMoved(Touch touch)
        {
            var dragDistance = Vector2.Distance(_dragStartPosition, touch.position);
            
            if (dragDistance > _dragThreshold && !_isDragging)
            {
                _isDragging = true;
                if (_debugInput)
                {
                    Debug.Log("Drag started");
                }
            }

            _lastTouchPosition = touch.position;
        }

        private void HandleTouchEnded(Touch touch)
        {
            if (_isDragging)
            {
                ProcessDrag(_dragStartPosition, touch.position);
                _isDragging = false;
            }
            else
            {
                ProcessClick(touch.position);
            }
        }

        private void ProcessClick(Vector2 screenPosition)
        {
            var worldHit = ScreenToWorldHit(screenPosition);
            Debug.Log($"World hit: {worldHit}");
    
            if (!worldHit.HasValue)
                return;

            var hexCoord = _hexGridManager.WorldToHex(worldHit.Value);
            Debug.Log($"Hex coord: {hexCoord}");
    
            if (_hexGridManager.IsValidHex(hexCoord))
            {
                Debug.Log($"Valid hex found");
                var tile = _hexGridManager.GetHexTile(hexCoord);
                Debug.Log($"Tile: {tile}, Type: {tile?.TileType}, IsWalkable: {tile?.IsWalkable}");
        
                if (tile != null && tile.IsWalkable)
                {
                    Debug.Log($"Triggering movement to {hexCoord}");
                    var worldPos = _hexGridManager.HexToWorld(hexCoord);
            
                    Debug.Log($"OnMapClicked subscribers: {OnMapClicked?.GetInvocationList()?.Length ?? 0}");
                    Debug.Log($"EventManager null? {_eventManager == null}");
            
                    OnMapClicked?.Invoke(worldPos);
                    _eventManager.TriggerHexClicked(hexCoord.ToOffset(), worldPos);
                }
                else
                {
                    Debug.Log($"Tile not walkable");
                }
            }
            else
            {
                Debug.Log($"Invalid hex");
            }
        }

        private void ProcessDoubleClick(Vector2 screenPosition)
        {
            var worldHit = ScreenToWorldHit(screenPosition);
            if (worldHit.HasValue)
            {
                OnMapDoubleClicked?.Invoke(worldHit.Value);
                
                if (_debugInput)
                {
                    Debug.Log($"Double clicked at: {worldHit.Value}");
                }
            }
        }

        private void ProcessAlternativeClick(Vector2 screenPosition)
        {
            var worldHit = ScreenToWorldHit(screenPosition);
            if (worldHit.HasValue)
            {
                if (_debugInput)
                {
                    Debug.Log($"Alternative click at: {worldHit.Value}");
                }
            }
        }

        private void ProcessDrag(Vector2 startScreen, Vector2 endScreen)
        {
            var startWorld = ScreenToWorldHit(startScreen);
            var endWorld = ScreenToWorldHit(endScreen);

            if (startWorld.HasValue && endWorld.HasValue)
            {
                OnMapDragged?.Invoke(startWorld.Value, endWorld.Value);
                
                if (_debugInput)
                {
                    Debug.Log($"Dragged from {startWorld.Value} to {endWorld.Value}");
                }
            }
        }

        private Vector3? ScreenToWorldHit(Vector2 screenPosition)
        {
            var ray = _mainCamera.ScreenPointToRay(screenPosition);
            
            if (Physics.Raycast(ray, out var hit))
            {
                return hit.point;
            }

            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var distance))
            {
                return ray.GetPoint(distance);
            }

            return null;
        }

        public void SetTouchSensitivity(float sensitivity)
        {
            _touchSensitivity = Mathf.Clamp01(sensitivity);
        }

        public void EnableMobileInput(bool enable)
        {
            _enableMobileInput = enable;
        }

        public void SetDebugMode(bool debug)
        {
            _debugInput = debug;
        }

        public void Dispose()
        {
            OnMapClicked = null;
            OnMapDoubleClicked = null;
            OnMapDragged = null;
        }

        private void OnDrawGizmosSelected()
        {
            if (_debugInput && _isDragging)
            {
                Gizmos.color = Color.red;
                var startWorld = ScreenToWorldHit(_dragStartPosition);
                var currentWorld = ScreenToWorldHit(_lastTouchPosition);
                
                if (startWorld.HasValue && currentWorld.HasValue)
                {
                    Gizmos.DrawLine(startWorld.Value, currentWorld.Value);
                    Gizmos.DrawWireSphere(startWorld.Value, 0.5f);
                    Gizmos.DrawWireSphere(currentWorld.Value, 0.5f);
                }
            }
        }
    }
}