using UnityEngine;
using UnityEngine.InputSystem;

namespace CostaLivre
{
    internal static class CostaUi
    {
        private static Vector2 pointer;
        private static bool active;
        private static bool click;
        private static bool held;

        internal static void Update()
        {
            click = held = false;
            Gamepad pad = Gamepad.current;
            if (pad == null || Mouse.current?.delta.ReadValue().sqrMagnitude > 0.1f)
                active = false;
            if (pad == null)
                return;
            Vector2 move = pad.leftStick.ReadValue();
            if (move.sqrMagnitude < 0.03f)
                move = pad.dpad.ReadValue();
            if (!active && (move.sqrMagnitude > 0.03f || pad.buttonSouth.wasPressedThisFrame))
            {
                active = true;
                Vector2 mouse = Mouse.current?.position.ReadValue() ?? new Vector2(Screen.width / 2f, Screen.height / 2f);
                pointer = new Vector2(mouse.x, Screen.height - mouse.y);
            }
            if (!active)
                return;
            pointer += new Vector2(move.x, -move.y) * (Mathf.Min(Screen.width, Screen.height) * 0.8f * Time.unscaledDeltaTime);
            pointer.x = Mathf.Clamp(pointer.x, 2, Screen.width - 2);
            pointer.y = Mathf.Clamp(pointer.y, 2, Screen.height - 2);
            click = pad.buttonSouth.wasPressedThisFrame;
            held = pad.buttonSouth.isPressed;
        }

        internal static bool Pick(Rect area)
        {
            if (!active || !click || !GUI.enabled || Event.current.type != EventType.Repaint || !area.Contains(Position))
                return false;
            click = false;
            return true;
        }

        internal static Vector2 Position => GUIUtility.ScreenToGUIPoint(pointer);

        internal static bool Button(Rect area, string label, GUIStyle style)
        {
            return GUI.Button(area, label, style) || Pick(area);
        }

        internal static float Slider(Rect area, float value, float min, float max)
        {
            float result = GUI.HorizontalSlider(area, value, min, max);
            if (active && held && Event.current.type == EventType.Repaint && area.Contains(Position))
                return Mathf.Lerp(min, max, (Position.x - area.x) / area.width);
            return result;
        }

        internal static void DrawPointer()
        {
            if (!active || Event.current.type != EventType.Repaint)
                return;
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            GUI.matrix = Matrix4x4.identity;
            GUI.color = new Color(1, 0.80f, 0.52f);
            GUI.DrawTexture(new Rect(pointer.x - 5, pointer.y - 1, 10, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(pointer.x - 1, pointer.y - 5, 2, 10), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.matrix = matrix;
        }
    }
}
