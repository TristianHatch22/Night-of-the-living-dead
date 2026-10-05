using UnityEngine;
using UnityEngine.InputSystem;

namespace NOTLD
{
    /// <summary>Keyboard/mouse + gamepad input in one place. Gamepad layout:
    /// LS move, RS look, RT fire, LT aim, A jump, X reload, Y swap weapon, B / LB use (hold to rebuild),
    /// RS-click knife, LS-click sprint, Start pause.</summary>
    public static class GameInput
    {
        public static bool UsingGamepad { get; private set; }
        static int detectFrame = -1;
        static float rumbleT, rumbleLow, rumbleHigh;
        static bool rumbling;

        public static string UseLabel { get { Detect(); return UsingGamepad ? "B" : "F"; } }

        public static void Detect()
        {
            if (detectFrame == Time.frameCount) return;
            detectFrame = Time.frameCount;
            var gp = Gamepad.current; var kb = Keyboard.current; var ms = Mouse.current;
            bool pad = gp != null && (gp.leftStick.ReadValue().sqrMagnitude > 0.1f || gp.rightStick.ReadValue().sqrMagnitude > 0.1f
                || gp.buttonSouth.isPressed || gp.buttonEast.isPressed || gp.buttonWest.isPressed || gp.buttonNorth.isPressed
                || gp.rightTrigger.isPressed || gp.leftTrigger.isPressed || gp.startButton.isPressed
                || gp.leftShoulder.isPressed || gp.rightShoulder.isPressed || gp.dpad.ReadValue().sqrMagnitude > 0.1f);
            bool pc = (kb != null && kb.anyKey.isPressed) || (ms != null && (ms.delta.ReadValue().sqrMagnitude > 1f || ms.leftButton.isPressed || ms.rightButton.isPressed));
            if (pad) UsingGamepad = true; else if (pc) UsingGamepad = false;
        }

        public static Vector2 Move()
        {
            Detect();
            var kb = Keyboard.current; var gp = Gamepad.current; Vector2 v = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) v.y += 1; if (kb.sKey.isPressed) v.y -= 1;
                if (kb.dKey.isPressed) v.x += 1; if (kb.aKey.isPressed) v.x -= 1;
            }
            if (gp != null) { Vector2 s = gp.leftStick.ReadValue(); if (s.magnitude > 0.2f) v += s; }
            return Vector2.ClampMagnitude(v, 1f);
        }

        /// <summary>Look input in degrees for this frame (before sensitivity).</summary>
        public static Vector2 Look(float dt)
        {
            Vector2 d = Vector2.zero; var ms = Mouse.current; var gp = Gamepad.current;
            if (ms != null) d += ms.delta.ReadValue() * 0.08f;
            if (gp != null)
            {
                Vector2 s = gp.rightStick.ReadValue(); float m = s.magnitude;
                if (m > 0.15f)
                {
                    float k = (m - 0.15f) / 0.85f; k = k * k;
                    d += s.normalized * k * dt * 210f;
                }
            }
            return d;
        }

        public static bool FireHeld() { var m = Mouse.current; var g = Gamepad.current; return (m != null && m.leftButton.isPressed) || (g != null && g.rightTrigger.isPressed); }
        public static bool FirePressed() { var m = Mouse.current; var g = Gamepad.current; return (m != null && m.leftButton.wasPressedThisFrame) || (g != null && g.rightTrigger.wasPressedThisFrame); }
        public static bool AimHeld() { var m = Mouse.current; var g = Gamepad.current; return (m != null && m.rightButton.isPressed) || (g != null && g.leftTrigger.isPressed); }
        public static bool ReloadPressed() { var k = Keyboard.current; var g = Gamepad.current; return (k != null && k.rKey.wasPressedThisFrame) || (g != null && g.buttonWest.wasPressedThisFrame); }
        public static bool JumpPressed() { var k = Keyboard.current; var g = Gamepad.current; return (k != null && k.spaceKey.wasPressedThisFrame) || (g != null && g.buttonSouth.wasPressedThisFrame); }
        public static bool SprintHeld() { var k = Keyboard.current; var g = Gamepad.current; return (k != null && k.leftShiftKey.isPressed) || (g != null && g.leftStickButton.isPressed); }
        public static bool MeleePressed() { var k = Keyboard.current; var m = Mouse.current; var g = Gamepad.current; return (k != null && k.vKey.wasPressedThisFrame) || (m != null && m.middleButton.wasPressedThisFrame) || (g != null && g.rightStickButton.wasPressedThisFrame); }
        public static bool UseHeld() { var k = Keyboard.current; var g = Gamepad.current; return (k != null && k.fKey.isPressed) || (g != null && (g.buttonEast.isPressed || g.leftShoulder.isPressed)); }
        public static bool UsePressed() { var k = Keyboard.current; var g = Gamepad.current; return (k != null && k.fKey.wasPressedThisFrame) || (g != null && (g.buttonEast.wasPressedThisFrame || g.leftShoulder.wasPressedThisFrame)); }
        public static bool PausePressed() { var k = Keyboard.current; var g = Gamepad.current; return (k != null && k.escapeKey.wasPressedThisFrame) || (g != null && g.startButton.wasPressedThisFrame); }
        public static bool GrenadePressed() { var k = Keyboard.current; var g = Gamepad.current; return (k != null && k.gKey.wasPressedThisFrame) || (g != null && g.dpad.up.wasPressedThisFrame); }
        public static bool WeaponSwapPressed() { var g = Gamepad.current; return g != null && (g.buttonNorth.wasPressedThisFrame || g.rightShoulder.wasPressedThisFrame); }
        public static float Scroll() { var m = Mouse.current; return m != null ? m.scroll.ReadValue().y : 0f; }
        public static int WeaponDigit()
        {
            var k = Keyboard.current; if (k == null) return -1;
            if (k.digit1Key.wasPressedThisFrame) return 0;
            if (k.digit2Key.wasPressedThisFrame) return 1;
            return -1;
        }

        // ---------------- rumble ----------------
        public static void Rumble(float low, float high, float seconds)
        {
            rumbleLow = Mathf.Clamp01(low); rumbleHigh = Mathf.Clamp01(high); rumbleT = seconds;
        }

        public static void TickRumble(float dt)
        {
            var g = Gamepad.current;
            if (g == null) return;
            if (rumbleT > 0) { rumbleT -= dt; g.SetMotorSpeeds(rumbleLow, rumbleHigh); rumbling = true; }
            else if (rumbling) { g.SetMotorSpeeds(0, 0); rumbling = false; }
        }

        public static void StopRumble()
        {
            rumbleT = 0;
            var g = Gamepad.current;
            if (g != null) g.SetMotorSpeeds(0, 0);
            rumbling = false;
        }
    }
}
