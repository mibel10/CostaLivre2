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
        private static float clickedAt = -10;
        private static int lastButton;
        private static readonly GUIContent content = new();

        internal static void Update(bool capturing)
        {
            click = held = false;
            if (Touchscreen.current != null && CostaTouch.Available)
            {
                foreach (var finger in Touchscreen.current.touches)
                {
                    if (!finger.press.wasPressedThisFrame && !finger.press.isPressed && !finger.press.wasReleasedThisFrame)
                        continue;
                    active = true;
                    pointer = CostaTouch.ScreenPoint(finger.position.ReadValue());
                    click = finger.press.wasPressedThisFrame;
                    held = finger.press.isPressed;
                    return;
                }
            }
            if (capturing)
                return;
            Gamepad pad = Gamepad.current;
            Mouse mouseInput = Mouse.current;
            if (pad == null || mouseInput?.delta.ReadValue().sqrMagnitude > 0.1f
                || mouseInput?.leftButton.wasPressedThisFrame == true)
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

        internal static bool PointerActive => active;
        internal static bool PointerPressed => active && click;
        internal static bool PointerHeld => active && held;

        internal static Vector2 Position => GUIUtility.ScreenToGUIPoint(pointer);

        internal static bool Hovering(Rect area) => active && area.Contains(Position);

        internal static bool Button(Rect area, string label, GUIStyle style)
        {
            int identity = unchecked(label.GetHashCode() * 397 ^ (int)area.x * 31 ^ (int)area.y);
            bool touch = CostaTouch.Available;
            bool pressed;
            if (!touch)
                pressed = GUI.Button(area, label, style) || Pick(area);
            else
            {
                if (Event.current.type == EventType.Repaint)
                {
                    content.text = label;
                    style.Draw(area, content, false, held && area.Contains(Position), false, false);
                }
                pressed = Pick(area);
            }
            if (pressed)
            {
                clickedAt = Time.unscaledTime;
                lastButton = identity;
            }
            float time = (Time.unscaledTime - clickedAt) * 5.5f;
            if (Event.current.type == EventType.Repaint && identity == lastButton && time < 1)
            {
                Color old = GUI.color;
                GUI.color = new Color(1,.83f,.59f,.24f * (1-time));
                GUI.DrawTexture(new Rect(area.x, area.yMax - 2, area.width * Mathf.Lerp(.44f, 1, time), 2),
                    Texture2D.whiteTexture);
                GUI.color = old;
            }
            return pressed;
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
            if (!active || CostaTouch.Available && Gamepad.current == null || Event.current.type != EventType.Repaint)
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
