using UnityEngine;

namespace Core.Camera
{
    public class CameraController : MonoBehaviour, ICameraController
    {
        [SerializeField] private Vector3 _offset = new Vector3(0, 10, -8);
        [SerializeField] private float _followSmoothing = 8f; // Much smoother
        [SerializeField] private bool _lockRotation = true; // Keep rotation fixed
        [SerializeField] private Transform _boatTransform; // Direct reference

        [Header("Grid Movement")]
        [SerializeField] private bool _snapToGrid = true;
        [SerializeField] private float _gridSnapThreshold = 0.5f;

        private Transform _target;
        private bool _isFollowing;
        private Quaternion _fixedRotation;

        private void Start()
        {
            // Store initial rotation
            _fixedRotation = transform.rotation;
            
            // Auto-follow boat if referenced
            if (_boatTransform != null)
            {
                FollowTarget(_boatTransform);
            }
        }

        public void FollowTarget(Transform target)
        {
            _target = target;
            _isFollowing = true;
        }

        public void StopFollowing()
        {
            _isFollowing = false;
            _target = null;
        }

        public void SetFollowSmoothing(float smoothing)
        {
            _followSmoothing = smoothing;
        }

        private void LateUpdate()
        {
            // Use direct boat reference if no target set
            var followTarget = _target ?? _boatTransform;
            
            if (_isFollowing && followTarget != null)
            {
                var targetPosition = followTarget.position + _offset;
                
                // Snap to grid if enabled
                if (_snapToGrid)
                {
                    var currentPos = transform.position;
                    var deltaX = Mathf.Abs(targetPosition.x - currentPos.x);
                    var deltaZ = Mathf.Abs(targetPosition.z - currentPos.z);
                    
                    // Only move if significant change
                    if (deltaX > _gridSnapThreshold || deltaZ > _gridSnapThreshold)
                    {
                        // Snap X and Z independently
                        if (deltaX > _gridSnapThreshold)
                            currentPos.x = Mathf.Lerp(currentPos.x, targetPosition.x, _followSmoothing * Time.deltaTime);
                        if (deltaZ > _gridSnapThreshold)
                            currentPos.z = Mathf.Lerp(currentPos.z, targetPosition.z, _followSmoothing * Time.deltaTime);
                        
                        currentPos.y = targetPosition.y; // Keep height constant
                        transform.position = currentPos;
                    }
                }
                else
                {
                    transform.position = Vector3.Lerp(transform.position, targetPosition, 
                        _followSmoothing * Time.deltaTime);
                }

                // Keep rotation fixed
                if (_lockRotation)
                {
                    transform.rotation = _fixedRotation;
                }
            }
        }
    }
}