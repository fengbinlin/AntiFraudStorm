using UnityEngine;

namespace AntiFraudStorm.Player
{
    /// <summary>
    /// Legacy Input Manager wrapper (WASD / arrows + mouse + sprint + jump).
    /// </summary>
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] string moveXAxis = "Horizontal";
        [SerializeField] string moveYAxis = "Vertical";
        [SerializeField] string lookXAxis = "Mouse X";
        [SerializeField] string lookYAxis = "Mouse Y";
        [SerializeField] KeyCode sprintKey = KeyCode.LeftShift;
        [SerializeField] KeyCode jumpKey = KeyCode.Space;
        [SerializeField] KeyCode cycleCameraKey = KeyCode.V;
        [SerializeField] KeyCode propCameraKey = KeyCode.C;
        [SerializeField] KeyCode interactKey = KeyCode.E;
        [SerializeField] bool lockCursor = true;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Sprint { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool CycleCameraPressed { get; private set; }
        public bool PropCameraPressed { get; private set; }
        public bool InteractPressed { get; private set; }

        public static PlayerInputReader Active { get; private set; }

        public void ClaimFocus()
        {
            if (!isActiveAndEnabled)
                return;

            Active = this;
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnEnable()
        {
            ClaimFocus();
        }

        void OnDisable()
        {
            if (Active == this)
                Active = null;
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update()
        {
            Move = new Vector2(Input.GetAxisRaw(moveXAxis), Input.GetAxisRaw(moveYAxis));
            if (Move.sqrMagnitude > 1f)
                Move.Normalize();

            Look = new Vector2(Input.GetAxis(lookXAxis), Input.GetAxis(lookYAxis));
            Sprint = Input.GetKey(sprintKey);
            JumpPressed = Input.GetKeyDown(jumpKey);
            CycleCameraPressed = Input.GetKeyDown(cycleCameraKey);
            PropCameraPressed = Input.GetKeyDown(propCameraKey);
            InteractPressed = Input.GetKeyDown(interactKey);

            if (lockCursor &&
                (Input.GetKeyDown(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt)))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }

            if (lockCursor && Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
