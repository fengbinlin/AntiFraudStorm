using UnityEngine;

namespace AntiFraudStorm.Player
{
    /// <summary>
    /// Orbit camera with spring-arm style collision (sphere cast pull-in).
    /// </summary>
    public class ThirdPersonSpringArm : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] Transform followTarget;
        [SerializeField] Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

        [Header("Orbit")]
        [SerializeField] float mouseSensitivity = 2.5f;
        [SerializeField] float minPitch = -30f;
        [SerializeField] float maxPitch = 70f;
        [SerializeField] float defaultDistance = 4.5f;
        [SerializeField] float minDistance = 0.6f;
        [SerializeField] float cameraCollisionRadius = 0.2f;
        [SerializeField] float collisionBuffer = 0.15f;
        [SerializeField] float zoomSmooth = 18f;
        [SerializeField] LayerMask collisionMask = ~0;

        [Header("Follow")]
        [SerializeField] float followSmooth = 20f;

        float _yaw;
        float _pitch = 15f;
        float _currentDistance;
        Transform _cameraTransform;

        public Transform CameraTransform => _cameraTransform;
        /// <summary>Horizontal look angle used for orbit (degrees). Prefer this for locomotion facing.</summary>
        public float PlanarYaw => _yaw;

        public void Initialize(Transform target, Transform cameraTransform)
        {
            followTarget = target;
            _cameraTransform = cameraTransform;
            _currentDistance = defaultDistance;

            if (followTarget != null)
            {
                _yaw = followTarget.eulerAngles.y;
                var offset = cameraTransform.position - GetPivotWorld();
                if (offset.sqrMagnitude > 0.01f)
                {
                    var euler = Quaternion.LookRotation(-offset.normalized).eulerAngles;
                    _yaw = euler.y;
                    _pitch = NormalizePitch(euler.x);
                }
            }
        }

        public void SetFollowTarget(Transform target) => followTarget = target;

        public void Tick(Vector2 lookInput, float deltaTime)
        {
            if (followTarget == null || _cameraTransform == null)
                return;

            _yaw += lookInput.x * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - lookInput.y * mouseSensitivity, minPitch, maxPitch);

            Vector3 pivot = GetPivotWorld();
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredDir = orbit * Vector3.back;

            float desiredDistance = defaultDistance;
            var hits = Physics.SphereCastAll(
                pivot,
                cameraCollisionRadius,
                desiredDir,
                defaultDistance + collisionBuffer,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            float closest = desiredDistance;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (followTarget != null && hit.transform != null &&
                    (hit.transform == followTarget || hit.transform.IsChildOf(followTarget)))
                    continue;

                if (hit.distance < closest)
                    closest = hit.distance;
            }

            if (closest < desiredDistance)
                desiredDistance = Mathf.Clamp(closest - collisionBuffer, minDistance, defaultDistance);

            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 1f - Mathf.Exp(-zoomSmooth * deltaTime));

            Vector3 desiredPos = pivot + desiredDir * _currentDistance;
            _cameraTransform.position = Vector3.Lerp(
                _cameraTransform.position, desiredPos, 1f - Mathf.Exp(-followSmooth * deltaTime));
            _cameraTransform.rotation = Quaternion.LookRotation(pivot - _cameraTransform.position, Vector3.up);
        }

        Vector3 GetPivotWorld()
        {
            return followTarget.TransformPoint(targetOffset);
        }

        static float NormalizePitch(float pitch)
        {
            if (pitch > 180f)
                pitch -= 360f;
            return pitch;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (followTarget == null)
                return;

            Gizmos.color = Color.cyan;
            Vector3 pivot = followTarget.TransformPoint(targetOffset);
            Gizmos.DrawWireSphere(pivot, 0.08f);
            Gizmos.DrawLine(pivot, pivot + Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.back * defaultDistance);
        }
#endif
    }
}
