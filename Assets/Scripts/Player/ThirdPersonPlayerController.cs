using Animancer;
using UnityEngine;

namespace AntiFraudStorm.Player
{
    /// <summary>
    /// Orchestrates input, motor, spring-arm camera, and Animancer locomotion.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class ThirdPersonPlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] ThirdPersonMotor motor;
        [SerializeField] ThirdPersonSpringArm springArm;
        [SerializeField] PlayerAnimancerLocomotion locomotion;
        [SerializeField] CharacterController characterController;
        [SerializeField] AnimancerComponent animancer;
        [SerializeField] Transform cameraTransform;

        bool _jumpPressed;

        void Awake()
        {
            ResolveReferences();
            WireUp();
        }

        void Update()
        {
            // Jump is edge-triggered — sample in Update before LateUpdate consumes it.
            if (input != null)
                _jumpPressed = input.JumpPressed;
        }

        void LateUpdate()
        {
            if (input == null)
                return;

            // Camera first, then move — so W aligns to the current look yaw this frame.
            springArm?.Tick(input.Look, Time.deltaTime);

            if (motor != null)
            {
                motor.Tick(input.Move, input.Sprint, _jumpPressed, Time.deltaTime);
                bool isMoving = motor.HorizontalVelocity.sqrMagnitude > 0.01f;
                locomotion?.Tick(isMoving, motor.IsRunning, motor.IsGrounded, _jumpPressed);
            }

            _jumpPressed = false;
        }

        void ResolveReferences()
        {
            if (input == null)
                input = GetComponent<PlayerInputReader>();
            if (motor == null)
                motor = GetComponent<ThirdPersonMotor>();
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (animancer == null)
                animancer = GetComponentInChildren<AnimancerComponent>();
            if (locomotion == null)
                locomotion = GetComponentInChildren<PlayerAnimancerLocomotion>();
            if (springArm == null)
                springArm = GetComponentInChildren<ThirdPersonSpringArm>(true);
            if (cameraTransform == null)
            {
                var cam = GetComponentInChildren<Camera>(true);
                if (cam != null)
                    cameraTransform = cam.transform;
            }
        }

        void WireUp()
        {
            if (springArm != null)
            {
                var cam = cameraTransform != null
                    ? cameraTransform
                    : GetComponentInChildren<Camera>(true)?.transform;

                // Prefab keeps CameraRig under Player for convenience; detach at runtime
                // so player yaw does not twist the orbit arm.
                if (springArm.transform.parent == transform)
                {
                    springArm.transform.SetParent(null, true);
                }

                springArm.Initialize(transform, cam);
                cameraTransform = cam;
            }

            if (motor != null)
                motor.Initialize(characterController, cameraTransform, springArm);

            if (locomotion != null && animancer != null)
                locomotion.Initialize(animancer);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();
            if (motor == null)
                motor = GetComponent<ThirdPersonMotor>();
            if (input == null)
                input = GetComponent<PlayerInputReader>();
        }
#endif
    }
}
