using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;   // GamepadButton
#endif

namespace MadrasGethu
{
    /// <summary>
    /// Reads keyboard/mouse and gamepad input directly, so no Input Actions asset is needed.
    /// Works whether the project uses the new Input System package (Unity 6 default)
    /// or the old Input Manager.
    ///
    /// Keyboard / Mouse                 Gamepad (Xbox layout)
    ///   WASD      move                   Left stick   move
    ///   Mouse     camera                 Right stick  camera
    ///   Shift     sprint                 LB           sprint
    ///   Space     jump                   A            jump
    ///   C / Ctrl  dodge (direction+C)    LT           dodge
    ///   LMB       light attack           X            light attack
    ///   RMB       heavy attack           Y            heavy attack
    ///   F         block / counter        RB           block / counter
    ///   E         grab / throw           B            grab / throw
    ///   Q         Gethu (finisher)       RT           Gethu (finisher)
    ///   Enter     restart                Start        restart
    ///   H         toggle controls help   Back/View    toggle controls help
    /// </summary>
    public static class GameInput
    {
        const float MouseDegreesPerPixel = 0.12f;
        const float StickDegreesPerSecond = 180f;

        /// <summary>Movement on the ground plane, x = right, y = forward. Magnitude 0..1.</summary>
        public static Vector2 Move
        {
            get
            {
                Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                }
                var pad = Gamepad.current;
                if (pad != null) v += pad.leftStick.ReadValue();
#else
                v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        /// <summary>Camera rotation for this frame in degrees (x = yaw, y = pitch).</summary>
        public static Vector2 Look
        {
            get
            {
                Vector2 v = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
                    v += mouse.delta.ReadValue() * MouseDegreesPerPixel;
                var pad = Gamepad.current;
                if (pad != null)
                    v += pad.rightStick.ReadValue() * StickDegreesPerSecond * Time.unscaledDeltaTime;
#else
                if (Cursor.lockState == CursorLockMode.Locked)
                    v = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 1.2f;
#endif
                return v;
            }
        }

        public static bool Sprint
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                    || (Gamepad.current != null && Gamepad.current.leftShoulder.isPressed);
#else
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton4);
#endif
            }
        }

        public static bool BlockHeld
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return (Keyboard.current != null && Keyboard.current.fKey.isPressed)
                    || (Gamepad.current != null && Gamepad.current.rightShoulder.isPressed);
#else
                return Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.JoystickButton5);
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        static bool KeyDown(Key key) => Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        static bool PadDown(GamepadButton button) => Gamepad.current != null && Gamepad.current[button].wasPressedThisFrame;

        public static bool BlockPressed => KeyDown(Key.F) || PadDown(GamepadButton.RightShoulder);
        public static bool JumpPressed => KeyDown(Key.Space) || PadDown(GamepadButton.South);
        public static bool DodgePressed => KeyDown(Key.C) || KeyDown(Key.LeftCtrl) || PadDown(GamepadButton.LeftTrigger);
        public static bool GrabPressed => KeyDown(Key.E) || PadDown(GamepadButton.East);
        public static bool GethuPressed => KeyDown(Key.Q) || PadDown(GamepadButton.RightTrigger);
        public static bool RestartPressed => KeyDown(Key.Enter) || PadDown(GamepadButton.Start);
        public static bool HelpPressed => KeyDown(Key.H) || PadDown(GamepadButton.Select);
        public static bool UnlockCursorPressed => KeyDown(Key.Escape);

        public static bool LightPressed =>
            (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && Mouse.current.leftButton.wasPressedThisFrame)
            || PadDown(GamepadButton.West);

        public static bool HeavyPressed =>
            (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked && Mouse.current.rightButton.wasPressedThisFrame)
            || PadDown(GamepadButton.North);

        public static bool ClickPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        public static bool BlockPressed => Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.JoystickButton5);
        public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
        public static bool DodgePressed => Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl);
        public static bool GrabPressed => Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.JoystickButton1);
        public static bool GethuPressed => Input.GetKeyDown(KeyCode.Q);
        public static bool RestartPressed => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton7);
        public static bool HelpPressed => Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.JoystickButton6);
        public static bool UnlockCursorPressed => Input.GetKeyDown(KeyCode.Escape);

        public static bool LightPressed =>
            (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(0)) || Input.GetKeyDown(KeyCode.JoystickButton2);

        public static bool HeavyPressed =>
            (Cursor.lockState == CursorLockMode.Locked && Input.GetMouseButtonDown(1)) || Input.GetKeyDown(KeyCode.JoystickButton3);

        public static bool ClickPressed => Input.GetMouseButtonDown(0);
#endif
    }
}
