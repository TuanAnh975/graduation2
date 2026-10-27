using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HanoiStreet
{
    [RequireComponent(typeof(Camera))]
    public sealed class FreeFlyCamera : MonoBehaviour
    {
        [Min(1)] public float speed = 14;
        [Min(1)] public float sprintMultiplier = 4;
        [Range(.02f, 1)] public float lookSensitivity = .12f;
        float pitch;
        float yaw;
        bool looking;
        bool suppressUntilRelease;

        void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0, transform.eulerAngles.x);
        }

        void Update()
        {
            Vector3 move = Vector3.zero;
            Vector2 mouse = Vector2.zero;
            bool held = false, escape = false, sprint = false;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            var pointer = Mouse.current;
            if (keyboard != null)
            {
                move = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0),
                    (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                escape = keyboard.escapeKey.wasPressedThisFrame;
            }
            if (pointer != null) { held = pointer.rightButton.isPressed; mouse = pointer.delta.ReadValue(); }
#elif ENABLE_LEGACY_INPUT_MANAGER
            move = new Vector3((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            held = Input.GetMouseButton(1);
            mouse = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10;
            sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            escape = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (escape) suppressUntilRelease = true;
            if (!held) suppressUntilRelease = false;
            bool nextLooking = held && !suppressUntilRelease && Application.isFocused;
            if (nextLooking != looking)
            {
                looking = nextLooking;
                Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !looking;
                mouse = Vector2.zero;
            }
            if (looking)
            {
                yaw += mouse.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - mouse.y * lookSensitivity, -85, 85);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            if (!Application.isFocused) return;
            Vector3 direction = transform.right * move.x + Vector3.up * move.y + transform.forward * move.z;
            transform.position += Vector3.ClampMagnitude(direction, 1) * speed * (sprint ? sprintMultiplier : 1) * Time.unscaledDeltaTime;
        }

        void OnApplicationFocus(bool focused) { if (!focused) ReleaseCursor(); }
        void OnDisable() { ReleaseCursor(); }
        void ReleaseCursor() { looking = false; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
