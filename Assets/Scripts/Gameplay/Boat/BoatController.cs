using System.Collections;
using System.Linq;
using UnityEngine;
using Core.HexGrid;
using Cysharp.Threading.Tasks;
using Infrastructure.Events;
using Zenject;

namespace Gameplay.Boat
{
    public class BoatController : MonoBehaviour, IBoatController
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 90f;
        
        [Header("Foam Effects")]
        [SerializeField] private GameObject _foamEffect;
        [SerializeField] private Material _foamMaterial;

        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IGameEventManager _eventManager;

        public Transform Transform => transform;
        public HexCoordinate CurrentHex { get; private set; }
        public bool IsMoving { get; private set; }

        private Coroutine _currentMovement;
        private float _foamPhase = 0f;

        public void MoveTo(HexCoordinate[] path)
        {
            if (path == null || path.Length == 0) return;

            // Stop current movement
            if (_currentMovement != null)
            {
                StopCoroutine(_currentMovement);
                IsMoving = false;
            }

            // Start new movement
            _currentMovement = StartCoroutine(MoveAlongPath(path));
        }

        private IEnumerator MoveAlongPath(HexCoordinate[] path)
        {
            IsMoving = true;
            
            // Enable foam effect
            SetFoamActive(true);

            // Trigger start event
            try
            {
                if (_eventManager != null && _hexGridManager != null)
                {
                    var worldPath = path.Select(hex => _hexGridManager.HexToWorld(hex)).ToArray();
                    _eventManager.TriggerBoatMovementStarted(worldPath);
                }
            }
            catch { }

            // Move through each point
            for (int i = 1; i < path.Length; i++)
            {
                if (_hexGridManager == null) break;

                var targetHex = path[i];
                var targetPos = _hexGridManager.HexToWorld(targetHex);

                yield return StartCoroutine(MoveToPosition(targetPos));
                CurrentHex = targetHex;
            }

            // Complete movement and disable foam
            IsMoving = false;
            _currentMovement = null;
            SetFoamActive(false);

            try
            {
                _eventManager?.TriggerBoatMovementCompleted(transform.position);
            }
            catch { }
        }

        private IEnumerator MoveToPosition(Vector3 targetPos)
        {
            var startPos = transform.position;
            var direction = (targetPos - startPos).normalized;

            // Rotate
            if (direction != Vector3.zero)
            {
                var targetRot = Quaternion.LookRotation(direction);
                var startRot = transform.rotation;
                var rotTime = 0f;
                var rotDuration = Quaternion.Angle(startRot, targetRot) / _rotationSpeed;

                while (rotTime < rotDuration)
                {
                    rotTime += Time.deltaTime;
                    var t = rotTime / rotDuration;
                    transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
                    yield return null;
                }
                transform.rotation = targetRot;
            }

            // Move
            var moveTime = 0f;
            var distance = Vector3.Distance(startPos, targetPos);
            var moveDuration = distance / _moveSpeed;

            while (moveTime < moveDuration)
            {
                moveTime += Time.deltaTime;
                var t = moveTime / moveDuration;
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                yield return null;
            }
            transform.position = targetPos;
        }

        public void CancelCurrentMovement()
        {
            if (_currentMovement != null)
            {
                StopCoroutine(_currentMovement);
                _currentMovement = null;
                IsMoving = false;
            }
            
            // Disable foam effect
            SetFoamActive(false);
        }

        private void SetFoamActive(bool active)
        {
            if (_foamEffect != null)
            {
                _foamEffect.SetActive(active);
                
                if (active && _foamMaterial != null)
                {
                    // Reset foam animation phase when starting
                    _foamPhase = 0f;
                    _foamMaterial.SetFloat("_Phase", _foamPhase);
                }
            }
        }

        private void Update()
        {
            // Animate foam while moving
            if (IsMoving && _foamMaterial != null)
            {
                _foamPhase += Time.deltaTime * 2f; // Animation speed
                _foamMaterial.SetFloat("_Phase", Mathf.PingPong(_foamPhase, 1f));
            }
        }

        public HexCoordinate GetCurrentHexFromPosition()
        {
            if (_hexGridManager != null)
                return _hexGridManager.WorldToHex(transform.position);
            return CurrentHex;
        }

        public void UpdateCurrentHex(HexCoordinate hex)
        {
            CurrentHex = hex;
        }

        public bool HasValidPosition()
        {
            return _hexGridManager?.IsWalkable(CurrentHex) ?? false;
        }

        public void SetPosition(HexCoordinate hex)
        {
            CancelCurrentMovement();
            CurrentHex = hex;
            if (_hexGridManager != null)
                transform.position = _hexGridManager.HexToWorld(hex);
        }

        // Wrapper for interface compatibility
        public async UniTask MoveToAsync(HexCoordinate[] path)
        {
            MoveTo(path);
            
            // Wait for movement to complete
            while (IsMoving)
            {
                await UniTask.NextFrame();
            }
        }

        private void OnDestroy()
        {
            CancelCurrentMovement();
        }
    }
}