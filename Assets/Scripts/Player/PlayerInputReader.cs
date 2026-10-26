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
        [SerializeField] bool lockCursor = true;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Sprint { get; private set; }
        public bool JumpPressed { get; private set; }

        void OnEnable()
        {
            if (lockCursor)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        void OnDisable()
        {
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

            if (lockCursor && Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (lockCursor && Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
