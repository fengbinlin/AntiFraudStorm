using System;
using UnityEngine;

namespace AntiFraudStorm.Player
{
    /// <summary>
    /// Camera rig: Third-Person (original stable orbit), First-Person, and Shoulder.
    /// Third-person path is isolated so other modes do not change its feel.
    /// </summary>
    public class ThirdPersonSpringArm : MonoBehaviour
    {
        [Serializable]
        public class ModeSettings
        {
            public Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
            [Tooltip("Orbit-local extras. Shoulder uses +X for right offset.")]
            public Vector3 armOffset = Vector3.zero;
            public float distance = 4.5f;
            public float minPitch = -30f;
            public float maxPitch = 70f;
            public float fieldOfView = 60f;
            public bool hidePlayerRenderers;
            public bool lockBodyToLookYaw;
        }

        [Header("Target")]
        [SerializeField] Transform followTarget;

        [Header("Modes")]
        [SerializeField] CameraViewMode mode = CameraViewMode.ThirdPerson;
        [SerializeField] ModeSettings thirdPerson = new ModeSettings
        {
            pivotOffset = new Vector3(0f, 1.55f, 0f),
            armOffset = Vector3.zero,
            distance = 4.5f,
            minPitch = -30f,
            maxPitch = 70f,
            fieldOfView = 60f
        };
        [SerializeField] ModeSettings firstPerson = new ModeSettings
        {
            pivotOffset = new Vector3(0f, 1.65f, 0f),
            armOffset = new Vector3(0f, 0f, 0.12f),
            distance = 0f,
            minPitch = -80f,
            maxPitch = 80f,
            fieldOfView = 75f,
            hidePlayerRenderers = true,
            lockBodyToLookYaw = true
        };
        [SerializeField] ModeSettings shoulder = new ModeSettings
        {
            pivotOffset = new Vector3(0f, 1.45f, 0f),
            armOffset = new Vector3(0.55f, 0.05f, 0f),
            distance = 2.2f,
            minPitch = -25f,
            maxPitch = 55f,
            fieldOfView = 55f
        };

        [Header("Orbit")]
        [SerializeField] float mouseSensitivity = 2.5f;
        [SerializeField] float minDistance = 0.6f;
        [SerializeField] float cameraCollisionRadius = 0.2f;
        [SerializeField] float collisionBuffer = 0.15f;
        [SerializeField] float zoomSmooth = 18f;
        [SerializeField] LayerMask collisionMask = ~0;

        [Header("Follow")]
        [SerializeField] float followSmooth = 20f;
        [SerializeField] float fovSmooth = 12f;

        float _yaw;
        float _pitch = 15f;
        float _currentDistance;
        float _shakeRemaining;
        float _shakeDuration;
        float _shakeAmplitude;
        Transform _cameraTransform;
        Camera _camera;
        ModeSettings _active;
        Renderer[] _playerRenderers;
        bool[] _rendererRestEnabled;
        Transform _keepVisibleRoot;

        public Transform CameraTransform => _cameraTransform;
        public float PlanarYaw => _yaw;
        public CameraViewMode Mode => mode;
        public bool LockBodyToLookYaw => _active != null && _active.lockBodyToLookYaw;

        public void Initialize(Transform target, Transform cameraTransform)
        {
            followTarget = target;
            _cameraTransform = cameraTransform;
            _camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            CachePlayerRenderers();
            ApplyMode(mode, instant: true);

            if (followTarget != null && cameraTransform != null)
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

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            CachePlayerRenderers();
        }

        /// <param name="keepVisibleRoot">
        /// When hiding player meshes (e.g. FP), renderers under this root stay visible
        /// (used for handheld prop camera).
        /// </param>
        public void SetMode(
            CameraViewMode newMode,
            bool instant = false,
            bool? hideRenderersOverride = null,
            Transform keepVisibleRoot = null)
        {
            _keepVisibleRoot = keepVisibleRoot;
            mode = newMode;
            ApplyMode(newMode, instant, hideRenderersOverride);
        }

        public void CycleMode()
        {
            // V-key view cycle never keeps a prop exception.
            SetMode((CameraViewMode)(((int)mode + 1) % 3), keepVisibleRoot: null);
        }

        public void AddShake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f)
                return;

            _shakeAmplitude = Mathf.Max(_shakeAmplitude, amplitude);
            _shakeDuration = Mathf.Max(_shakeDuration, duration);
            _shakeRemaining = Mathf.Max(_shakeRemaining, duration);
        }

        void ApplyMode(CameraViewMode newMode, bool instant, bool? hideRenderersOverride = null)
        {
            _active = ResolveSettings(newMode);
            _pitch = Mathf.Clamp(_pitch, _active.minPitch, _active.maxPitch);

            if (instant)
            {
                _currentDistance = Mathf.Max(0f, _active.distance);
                if (_camera != null)
                    _camera.fieldOfView = _active.fieldOfView;
            }

            bool hide = hideRenderersOverride ?? _active.hidePlayerRenderers;
            ApplyRendererVisibility(hide);
        }

        ModeSettings ResolveSettings(CameraViewMode m)
        {
            return m switch
            {
                CameraViewMode.FirstPerson => firstPerson,
                CameraViewMode.Shoulder => shoulder,
                _ => thirdPerson
            };
        }

        public void Tick(Vector2 lookInput, float deltaTime)
        {
            if (followTarget == null || _cameraTransform == null || _active == null)
                return;

            _yaw += lookInput.x * mouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - lookInput.y * mouseSensitivity, _active.minPitch, _active.maxPitch);

            switch (mode)
            {
                case CameraViewMode.FirstPerson:
                    TickFirstPerson(deltaTime);
                    break;
                case CameraViewMode.Shoulder:
                    TickShoulder(deltaTime);
                    break;
                default:
                    TickThirdPerson(deltaTime);
                    break;
            }

            if (_camera != null)
            {
                _camera.fieldOfView = Mathf.Lerp(
                    _camera.fieldOfView,
                    _active.fieldOfView,
                    1f - Mathf.Exp(-fovSmooth * deltaTime));
            }

            ApplyShake(deltaTime);
        }

        void ApplyShake(float deltaTime)
        {
            if (_shakeRemaining <= 0f || _cameraTransform == null)
                return;

            _shakeRemaining = Mathf.Max(0f, _shakeRemaining - deltaTime);
            float strength = _shakeDuration > 0f
                ? _shakeRemaining / _shakeDuration
                : 0f;
            strength *= strength;
            if (strength <= 0f)
            {
                _shakeAmplitude = 0f;
                _shakeDuration = 0f;
                return;
            }

            float noiseTime = Time.unscaledTime * 42f;
            float x = Mathf.PerlinNoise(noiseTime, 1.37f) * 2f - 1f;
            float y = Mathf.PerlinNoise(2.91f, noiseTime) * 2f - 1f;
            Vector3 offset =
                (_cameraTransform.right * x +
                 _cameraTransform.up * y) *
                (_shakeAmplitude * strength);
            _cameraTransform.position += offset;
            _cameraTransform.rotation *=
                Quaternion.Euler(y * 0.42f * strength, x * 0.34f * strength, 0f);
        }

        /// <summary>Original stable third-person orbit (unchanged behavior).</summary>
        void TickThirdPerson(float deltaTime)
        {
            Vector3 pivot = GetPivotWorld();
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredDir = orbit * Vector3.back;

            float desiredDistance = thirdPerson.distance;
            float blocked = ResolveCollisionDistance(pivot, desiredDir, desiredDistance);
            _currentDistance = Mathf.Lerp(
                _currentDistance, blocked, 1f - Mathf.Exp(-zoomSmooth * deltaTime));

            Vector3 desiredPos = pivot + desiredDir * _currentDistance;
            _cameraTransform.position = Vector3.Lerp(
                _cameraTransform.position, desiredPos, 1f - Mathf.Exp(-followSmooth * deltaTime));
            _cameraTransform.rotation = Quaternion.LookRotation(
                pivot - _cameraTransform.position, Vector3.up);
        }

        void TickShoulder(float deltaTime)
        {
            Vector3 pivot = GetPivotWorld();
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 localOffset = shoulder.armOffset + Vector3.back * shoulder.distance;
            Vector3 worldOffset = orbit * localOffset;
            float mag = worldOffset.magnitude;
            Vector3 dir = mag > 0.001f ? worldOffset / mag : orbit * Vector3.back;

            float blocked = ResolveCollisionDistance(pivot, dir, mag);
            _currentDistance = Mathf.Lerp(
                _currentDistance, blocked, 1f - Mathf.Exp(-zoomSmooth * deltaTime));

            Vector3 desiredPos = pivot + dir * _currentDistance;
            _cameraTransform.position = Vector3.Lerp(
                _cameraTransform.position, desiredPos, 1f - Mathf.Exp(-followSmooth * deltaTime));
            _cameraTransform.rotation = Quaternion.LookRotation(
                pivot - _cameraTransform.position, Vector3.up);
        }

        void TickFirstPerson(float deltaTime)
        {
            Vector3 pivot = GetPivotWorld();
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 desiredPos = pivot + orbit * firstPerson.armOffset;

            float t = 1f - Mathf.Exp(-followSmooth * 1.5f * deltaTime);
            _cameraTransform.position = Vector3.Lerp(_cameraTransform.position, desiredPos, t);
            _cameraTransform.rotation = Quaternion.Slerp(_cameraTransform.rotation, orbit, t);
            _currentDistance = 0f;
        }

        float ResolveCollisionDistance(Vector3 pivot, Vector3 desiredDir, float desiredDistance)
        {
            if (desiredDistance <= minDistance)
                return Mathf.Max(0f, desiredDistance);

            var hits = Physics.SphereCastAll(
                pivot,
                cameraCollisionRadius,
                desiredDir,
                desiredDistance + collisionBuffer,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            float closest = desiredDistance;
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit.transform != null &&
                    (hit.transform == followTarget || hit.transform.IsChildOf(followTarget)))
                    continue;

                if (hit.distance < closest)
                    closest = hit.distance;
            }

            if (closest < desiredDistance)
                return Mathf.Clamp(closest - collisionBuffer, minDistance, desiredDistance);
            return desiredDistance;
        }

        Vector3 GetPivotWorld()
        {
            return followTarget.TransformPoint(_active.pivotOffset);
        }

        void CachePlayerRenderers()
        {
            if (followTarget == null)
            {
                _playerRenderers = Array.Empty<Renderer>();
                _rendererRestEnabled = Array.Empty<bool>();
                return;
            }

            _playerRenderers = followTarget.GetComponentsInChildren<Renderer>(true);
            _rendererRestEnabled = new bool[_playerRenderers.Length];
            for (int i = 0; i < _playerRenderers.Length; i++)
                _rendererRestEnabled[i] = _playerRenderers[i].enabled;
        }

        void ApplyRendererVisibility(bool hide)
        {
            if (_playerRenderers == null)
                return;

            for (int i = 0; i < _playerRenderers.Length; i++)
            {
                var r = _playerRenderers[i];
                if (r == null)
                    continue;
                if (_cameraTransform != null && r.transform.IsChildOf(_cameraTransform))
                    continue;

                // Handheld prop (or any keep-visible root) must stay on in FP.
                if (hide && _keepVisibleRoot != null &&
                    (r.transform == _keepVisibleRoot || r.transform.IsChildOf(_keepVisibleRoot)))
                {
                    r.enabled = true;
                    continue;
                }

                if (hide)
                    r.enabled = false;
                else
                    r.enabled = _rendererRestEnabled[i];
            }
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

            var settings = ResolveSettings(mode);
            Gizmos.color = Color.cyan;
            Vector3 pivot = followTarget.TransformPoint(settings.pivotOffset);
            Gizmos.DrawWireSphere(pivot, 0.08f);
            Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 tip = pivot + orbit * (settings.armOffset + Vector3.back * settings.distance);
            Gizmos.DrawLine(pivot, tip);
        }
#endif
    }
}
