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
            HandleMouseInput();
            HandleMouseDrag();
        }

        private void HandleMouseInput()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                ProcessClick(UnityEngine.Input.mousePosition);
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                ProcessAlternativeClick(UnityEngine.Input.mousePosition);
            }
        }

        private void HandleMouseDrag()
        {
            if (UnityEngine.Input.GetMouseButtonDown(0))
            {
                StartDrag(UnityEngine.Input.mousePosition);
            }
            else if (UnityEngine.Input.GetMouseButton(0))
            {
                UpdateDrag(UnityEngine.Input.mousePosition);
            }
            else if (UnityEngine.Input.GetMouseButtonUp(0) && _isDragging)
            {
                EndDrag(UnityEngine.Input.mousePosition);
            }
        }

        private void StartDrag(Vector3 position)
        {
            _isDragging = false;
            _dragStartPosition = position;
        }

        private void UpdateDrag(Vector3 currentPosition)
        {
            var dragDistance = Vector3.Distance(_dragStartPosition, currentPosition);
            
            if (dragDistance > _dragThreshold && !_isDragging)
            {
                _isDragging = true;
            }
        }

        private void EndDrag(Vector3 endPosition)
        {
            ProcessDrag(_dragStartPosition, endPosition);
            _isDragging = false;
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

            CheckForDoubleTap(touch);
            UpdateTapTracking(touch);
        }

        private void CheckForDoubleTap(Touch touch)
        {
            float timeSinceLastTap = Time.time - _lastTapTime;
            float distanceFromLastTap = Vector2.Distance(touch.position, _lastTapPosition);

            if (timeSinceLastTap < _doubleTapTime && distanceFromLastTap < _dragThreshold)
            {
                ProcessDoubleClick(touch.position);
            }
        }

        private void UpdateTapTracking(Touch touch)
        {
            _lastTapTime = Time.time;
            _lastTapPosition = touch.position;
        }

        private void HandleTouchMoved(Touch touch)
        {
            var dragDistance = Vector2.Distance(_dragStartPosition, touch.position);
            
            if (dragDistance > _dragThreshold && !_isDragging)
            {
                _isDragging = true;
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
            if (!worldHit.HasValue) return;

            var hexCoord = _hexGridManager.WorldToHex(worldHit.Value);

            if (_hexGridManager.IsValidHex(hexCoord))
            {
                var worldPos = _hexGridManager.HexToWorld(hexCoord);
                
                OnMapClicked?.Invoke(worldPos);
                _eventManager.TriggerHexClicked(hexCoord.ToOffset(), worldPos);
            }
        }

        private void ProcessDoubleClick(Vector2 screenPosition)
        {
            var worldHit = ScreenToWorldHit(screenPosition);
            if (worldHit.HasValue)
            {
                OnMapDoubleClicked?.Invoke(worldHit.Value);
            }
        }

        private void ProcessAlternativeClick(Vector2 screenPosition)
        {
            var worldHit = ScreenToWorldHit(screenPosition);
        }

        private void ProcessDrag(Vector2 startScreen, Vector2 endScreen)
        {
            var startWorld = ScreenToWorldHit(startScreen);
            var endWorld = ScreenToWorldHit(endScreen);

            if (startWorld.HasValue && endWorld.HasValue)
            {
                OnMapDragged?.Invoke(startWorld.Value, endWorld.Value);
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

        public void Dispose()
        {
            OnMapClicked = null;
            OnMapDoubleClicked = null;
            OnMapDragged = null;
        }
    }
}