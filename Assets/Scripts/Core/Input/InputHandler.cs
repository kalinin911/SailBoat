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

        // Touch handling variables
        private float _lastTapTime;
        private Vector3 _lastTapPosition;
        private bool _isDragging;
        private Vector3 _dragStartPosition;
        private Vector2 _lastTouchPosition;

        public void Initialize()
        {
            // Initialize input settings
            if (Application.isMobilePlatform)
            {
                _enableMobileInput = true;
                UnityEngine.Input.multiTouchEnabled = false; // Disable multitouch for simplicity
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
            // Left mouse button click
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                ProcessClick(UnityEngine.Input.mousePosition);
            }

            // Right mouse button for alternative action
            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                ProcessAlternativeClick(UnityEngine.Input.mousePosition);
            }

            // Mouse drag
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

            // Check for double tap
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
                // Simple tap
                ProcessClick(touch.position);
            }
        }

        private void ProcessClick(Vector2 screenPosition)
        {
            var worldHit = ScreenToWorldHit(screenPosition);
            if (worldHit.HasValue)
            {
                var hexCoord = _hexGridManager.WorldToHex(worldHit.Value);
                
                if (_hexGridManager.IsValidHex(hexCoord))
                {
                    var worldPos = _hexGridManager.HexToWorld(hexCoord);
                    
                    OnMapClicked?.Invoke(worldPos);
                    _eventManager.TriggerHexClicked(hexCoord.ToOffset(), worldPos);

                    if (_debugInput)
                    {
                        Debug.Log($"Clicked hex: {hexCoord} at world pos: {worldPos}");
                    }
                }
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
                // Could be used for showing hex info, context menu, etc.
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

            // Fallback: raycast to Y=0 plane
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