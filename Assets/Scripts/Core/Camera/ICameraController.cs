using UnityEngine;

namespace Core.Camera
{
    public interface ICameraController
    {
        void FollowTarget(Transform target);
        void StopFollowing();
        void SetFollowSmoothing(float smoothing);
    }
}