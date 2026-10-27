using UnityEngine;

namespace AntiFraudStorm.Player
{
    /// <summary>
    /// CharacterController locomotion relative to camera yaw.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ThirdPersonMotor : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] float walkSpeed = 4.2f;
        [SerializeField] float runSpeed = 7f;
        [SerializeField] float rotationSpeed = 900f;
        [SerializeField] float acceleration = 18f;

        [Header("Jump / Gravity")]
        [SerializeField] float jumpHeight = 1.2f;
        [SerializeField] float gravity = -20f;
        [SerializeField] float groundedGravity = -2f;

        CharacterController _controller;
        Transform _camera;
        ThirdPersonSpringArm _springArm;
        Vector3 _velocity;
        float _verticalVelocity;
        float _currentHorizontalSpeed;

        public Vector3 HorizontalVelocity => new Vector3(_velocity.x, 0f, _velocity.z);
        public float NormalizedSpeed { get; private set; }
        public bool IsGrounded => _controller != null && _controller.isGrounded;
        public bool IsRunning { get; private set; }

        public void Initialize(CharacterController controller, Transform cameraTransform, ThirdPersonSpringArm springArm = null)
        {
            _controller = controller;
            _camera = cameraTransform;
            _springArm = springArm;
        }

        public void Tick(Vector2 moveInput, bool sprint, bool jumpPressed, float deltaTime, bool lockBodyToLookYaw = false)
        {
            if (_controller == null)
                return;

            UpdateGrounding();
            ApplyJump(jumpPressed);
            ApplyGravity(deltaTime);
            MoveHorizontal(moveInput, sprint, deltaTime, lockBodyToLookYaw);

            var motion = _velocity + Vector3.up * _verticalVelocity;
            _controller.Move(motion * deltaTime);
        }

        void UpdateGrounding()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = groundedGravity;
        }

        void ApplyJump(bool jumpPressed)
        {
            if (!jumpPressed || !_controller.isGrounded)
                return;

            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        void ApplyGravity(float deltaTime)
        {
            _verticalVelocity += gravity * deltaTime;
        }

        void MoveHorizontal(Vector2 moveInput, bool sprint, float deltaTime, bool lockBodyToLookYaw)
        {
            IsRunning = sprint && moveInput.sqrMagnitude > 0.01f;
            float targetSpeed = moveInput.sqrMagnitude < 0.01f
                ? 0f
                : (IsRunning ? runSpeed : walkSpeed);

            _currentHorizontalSpeed = Mathf.MoveTowards(
                _currentHorizontalSpeed, targetSpeed, acceleration * deltaTime);

            float yaw = GetPlanarYaw();
            Vector3 moveDir = GetCameraRelativeDirection(moveInput, yaw);

            if (lockBodyToLookYaw)
            {
                var lookRot = Quaternion.Euler(0f, yaw, 0f);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, lookRot, rotationSpeed * deltaTime);
            }
            else if (moveDir.sqrMagnitude > 0.001f)
            {
                var targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, rotationSpeed * deltaTime);
            }

            _velocity = moveDir * _currentHorizontalSpeed;
            NormalizedSpeed = runSpeed > 0.01f
                ? Mathf.Clamp01(_currentHorizontalSpeed / runSpeed)
                : 0f;
        }

        float GetPlanarYaw()
        {
            if (_springArm != null)
                return _springArm.PlanarYaw;
            if (_camera != null)
                return _camera.eulerAngles.y;
            return transform.eulerAngles.y;
        }

        Vector3 GetCameraRelativeDirection(Vector2 moveInput, float yaw)
        {
            if (moveInput.sqrMagnitude < 0.0001f)
                return Vector3.zero;

            Vector3 world = Quaternion.Euler(0f, yaw, 0f) * new Vector3(moveInput.x, 0f, moveInput.y);
            return world.normalized;
        }
    }
}
