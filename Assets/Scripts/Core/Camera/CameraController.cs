using UnityEngine;

namespace Core.Camera
{
    public class CameraController : MonoBehaviour, ICameraController
    {
        [SerializeField] private Vector3 _offset = new Vector3(0, 10, -8);
        [SerializeField] private float _followSmoothing = 2f;
        [SerializeField] private bool _lookAtTarget = true;

        private Transform _target;
        private bool _isFollowing;

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
            if (_isFollowing && _target != null)
            {
                var targetPosition = _target.position + _offset;
                transform.position = Vector3.Lerp(transform.position, targetPosition, 
                    _followSmoothing * Time.deltaTime);

                if (_lookAtTarget)
                {
                    var lookDirection = _target.position - transform.position;
                    if (lookDirection != Vector3.zero)
                    {
                        var lookRotation = Quaternion.LookRotation(lookDirection);
                        transform.rotation = Quaternion.Lerp(transform.rotation, lookRotation, 
                            _followSmoothing * Time.deltaTime);
                    }
                }
            }
        }
    }
}