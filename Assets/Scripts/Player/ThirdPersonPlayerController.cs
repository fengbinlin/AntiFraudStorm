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
        [SerializeField] PlayerHandheldCamera handheldCamera;

        bool _jumpPressed;

        public Camera GameplayCamera
        {
            get
            {
                if (cameraTransform != null)
                {
                    Camera camera = cameraTransform.GetComponent<Camera>();
                    if (camera != null)
                        return camera;
                }

                return springArm != null
                    ? springArm.GetComponentInChildren<Camera>(true)
                    : null;
            }
        }

        void Awake()
        {
            ResolveReferences();
            WireUp();
        }

        void Update()
        {
            if (input != null)
                _jumpPressed = input.JumpPressed;
        }

        void LateUpdate()
        {
            if (input == null)
                return;

            if (AntiFraudStorm.SceneFlow.CampusComputerSession.IsFrozen)
            {
                _jumpPressed = false;
                return;
            }

            if (input.PropCameraPressed)
                handheldCamera?.TryToggle();
            else if (input.CycleCameraPressed &&
                     (handheldCamera == null || (!handheldCamera.IsHolding && !handheldCamera.IsBusy)))
                springArm?.CycleMode();

            springArm?.Tick(input.Look, Time.deltaTime);

            if (motor != null)
            {
                bool lockBody = springArm != null && springArm.LockBodyToLookYaw;
                bool busy = handheldCamera != null && handheldCamera.IsBusy;
                Vector2 move = busy ? Vector2.zero : input.Move;
                motor.Tick(move, busy ? false : input.Sprint, busy ? false : _jumpPressed, Time.deltaTime, lockBody);
                bool isMoving = motor.HorizontalVelocity.sqrMagnitude > 0.01f;
                locomotion?.Tick(isMoving, motor.IsRunning, motor.IsGrounded, busy ? false : _jumpPressed);
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
            if (handheldCamera == null)
                handheldCamera = GetComponent<PlayerHandheldCamera>();
            if (cameraTransform == null)
            {
                if (springArm != null)
                {
                    var rigCam = springArm.GetComponentInChildren<Camera>(true);
                    if (rigCam != null)
                        cameraTransform = rigCam.transform;
                }
                if (cameraTransform == null && Camera.main != null)
                    cameraTransform = Camera.main.transform;
            }
        }

        void WireUp()
        {
            if (springArm != null)
            {
                var cam = cameraTransform != null
                    ? cameraTransform
                    : springArm.GetComponentInChildren<Camera>(true)?.transform;

                if (springArm.transform.parent == transform)
                    springArm.transform.SetParent(null, true);

                springArm.Initialize(transform, cam);
                cameraTransform = cam;
            }

            if (motor != null)
                motor.Initialize(characterController, cameraTransform, springArm);

            if (locomotion != null && animancer != null)
                locomotion.Initialize(animancer);

            if (handheldCamera == null)
                handheldCamera = gameObject.AddComponent<PlayerHandheldCamera>();
            handheldCamera.Initialize(animancer, springArm, locomotion);
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
