using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.HexGrid;
using Infrastructure.Events;
using Zenject;

namespace Gameplay.Boat
{
    public class BoatController : MonoBehaviour, IBoatController
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 90f;

        [Inject] private IHexGridManager _hexGridManager;
        [Inject] private IGameEventManager _eventManager;

        public Transform Transform => transform;
        public HexCoordinate CurrentHex { get; private set; }
        public bool IsMoving { get; private set; }

        public async UniTask MoveToAsync(HexCoordinate[] path)
        {
            if (path.Length == 0 || IsMoving)
                return;

            IsMoving = true;
            var worldPath = path.Select(hex => _hexGridManager.HexToWorld(hex)).ToArray();
            
            _eventManager.TriggerBoatMovementStarted(worldPath);

            for (int i = 1; i < path.Length; i++)
            {
                var targetHex = path[i];
                var targetWorldPos = _hexGridManager.HexToWorld(targetHex);
                
                await MoveToPositionAsync(targetWorldPos);
                CurrentHex = targetHex;
            }

            IsMoving = false;
            _eventManager.TriggerBoatMovementCompleted(transform.position);
        }

        private async UniTask MoveToPositionAsync(Vector3 targetPosition)
        {
            var startPosition = transform.position;
            var direction = (targetPosition - startPosition).normalized;
            
            // Rotate towards target
            if (direction != Vector3.zero)
            {
                var targetRotation = Quaternion.LookRotation(direction);
                var startRotation = transform.rotation;
                
                var rotationTime = 0f;
                var rotationDuration = Quaternion.Angle(startRotation, targetRotation) / _rotationSpeed;
                
                while (rotationTime < rotationDuration)
                {
                    rotationTime += Time.deltaTime;
                    var t = rotationTime / rotationDuration;
                    transform.rotation = Quaternion.Lerp(startRotation, targetRotation, t);
                    await UniTask.NextFrame();
                }
                
                transform.rotation = targetRotation;
            }

            // Move to target
            var moveTime = 0f;
            var distance = Vector3.Distance(startPosition, targetPosition);
            var moveDuration = distance / _moveSpeed;

            while (moveTime < moveDuration)
            {
                moveTime += Time.deltaTime;
                var t = moveTime / moveDuration;
                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                await UniTask.NextFrame();
            }

            transform.position = targetPosition;
        }

        public void SetPosition(HexCoordinate hex)
        {
            CurrentHex = hex;
            transform.position = _hexGridManager.HexToWorld(hex);
        }

        [Inject]
        public void Construct(IHexGridManager hexGridManager, IGameEventManager eventManager)
        {
            _hexGridManager = hexGridManager;
            _eventManager = eventManager;
        }
    }
}