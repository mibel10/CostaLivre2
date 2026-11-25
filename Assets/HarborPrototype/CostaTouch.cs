using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaTouch
    {
        internal enum Control { Move, Sprint, Crouch, Jump, Roll, Use, Aim, Fire, Reload, Swap, Map, Bag, Menu, Retry }
        private static readonly string[] English = { "Move", "Run", "Crouch", "Jump", "Roll", "Use", "Aim", "Fire", "Reload", "Swap", "Map", "Bag", "Menu", "Retry" };
        private static readonly string[] PortugueseNames = { "Mover", "Correr", "Agachar", "Pular", "Rolar", "Usar", "Mirar", "Atirar", "Recarregar", "Trocar", "Mapa", "Mochila", "Menu", "Repetir" };
        private static readonly Vector2[] Centers = {
            new(.13f,.76f), new(.24f,.57f), new(.66f,.88f), new(.89f,.77f), new(.77f,.80f),
            new(.91f,.56f), new(.80f,.61f), new(.92f,.39f), new(.72f,.70f), new(.58f,.87f),
            new(.76f,.13f), new(.85f,.13f), new(.94f,.13f), new(.50f,.77f)
        };
        private readonly float[] sizes = new float[Centers.Length];
        private readonly bool[] down = new bool[Centers.Length];
        private readonly bool[] pressed = new bool[Centers.Length];
        private readonly int[] fingers = new int[Centers.Length];
        private int lookFinger = -1;
        private int selected;
        private GUIStyle caption;
        internal Vector2 Move { get; private set; }
        internal Vector2 Look { get; private set; }
        internal static bool Available => Application.isMobilePlatform && Touchscreen.current != null;

        internal CostaTouch()
        {
            for (int i = 0; i < sizes.Length; i++)
                sizes[i] = Mathf.Clamp(PlayerPrefs.GetFloat($"CostaTouchSize{i}", DefaultSize(i)), .065f, i == 0 ? .34f : .18f);
            ResetInput();
        }

        private static float DefaultSize(int i) => i == 0 ? .28f : i == (int)Control.Fire ? .15f : .105f;

        internal bool Down(Control control) => down[(int)control];
        internal bool Pressed(Control control) => pressed[(int)control];

        internal void ResetInput()
        {
            Array.Fill(fingers, -1);
            Array.Clear(down, 0, down.Length);
            Array.Clear(pressed, 0, pressed.Length);
            lookFinger = -1;
            Move = Look = Vector2.zero;
        }

        internal void Update(bool gameplay, bool driving, bool retry)
        {
            if (!Available || !gameplay)
            {
                ResetInput();
                return;
            }

            Array.Clear(pressed, 0, pressed.Length);
            Array.Clear(down, 0, down.Length);
            Move = Look = Vector2.zero;
            bool keepLook = false;
            foreach (var finger in Touchscreen.current.touches)
            {
                int id = finger.touchId.ReadValue();
                int button = Array.IndexOf(fingers, id);
                if (!finger.press.isPressed)
                {
                    if (button >= 0)
                        fingers[button] = -1;
                    if (lookFinger == id)
                        lookFinger = -1;
                    continue;
                }

                Vector2 p = ScreenPoint(finger.position.ReadValue());
                if (button < 0 && lookFinger != id && finger.press.wasPressedThisFrame)
                {
                    for (int i = sizes.Length - 1; i >= 0; i--)
                    {
                        if (fingers[i] < 0 && Visible(i, driving, retry) && Bounds(i).Contains(p))
                        {
                            button = i;
                            fingers[i] = id;
                            pressed[i] = true;
                            break;
                        }
                    }
                    if (button < 0 && lookFinger < 0 && p.x > Screen.width * .33f && !retry)
                        lookFinger = id;
                }
                if (button >= 0 && Visible(button, driving, retry))
                {
                    down[button] = true;
                    if (button == (int)Control.Move)
                    {
                        Rect area = Bounds(button);
                        Vector2 offset = p - area.center;
                        Move = Vector2.ClampMagnitude(new Vector2(offset.x, -offset.y) / (area.width * .43f), 1);
                    }
                }
                if (lookFinger == id)
                {
                    Look = finger.delta.ReadValue();
                    keepLook = true;
                }
            }
            if (!keepLook)
                lookFinger = -1;
        }

        private static bool Visible(int index, bool driving, bool retry)
        {
            Control control = (Control)index;
            if (retry)
                return control == Control.Retry || control == Control.Menu;
            return control != Control.Retry && (!driving || control == Control.Move || control == Control.Jump
                || control == Control.Use || control == Control.Menu || control == Control.Map || control == Control.Bag);
        }

        private Rect Bounds(int i)
        {
            Rect safe = Screen.safeArea;
            float size = Mathf.Min(safe.width, safe.height) * sizes[i];
            Vector2 p = new(safe.x + safe.width * Centers[i].x, Screen.height - safe.yMax + safe.height * Centers[i].y);
            return new Rect(Mathf.Clamp(p.x - size * .5f, safe.xMin, safe.xMax - size),
                Mathf.Clamp(p.y - size * .5f, Screen.height - safe.yMax, Screen.height - safe.yMin - size), size, size);
        }

        internal static Vector2 ScreenPoint(Vector2 p) => new(p.x, Screen.height - p.y);
        private static string Title(int i) => L(English[i], PortugueseNames[i]);

        internal void Draw(bool driving, bool retry)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            for (int i = 0; i < sizes.Length; i++)
                if (Visible(i, driving, retry))
                    DrawButton(i, down[i], driving);
        }

        private void DrawButton(int i, bool highlighted, bool driving)
        {
            caption ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            Rect area = Bounds(i);
            var previous = GUI.color;
            GUI.color = highlighted ? new Color(.9f, .55f, .25f, .83f) : new Color(.07f, .12f, .16f, .65f);
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = Color.white;
            string label = driving && i == (int)Control.Jump ? L("Brake", "Freio") : Title(i);
            float fontScale = Mathf.Min(.22f, 1.5f / label.Length);
            caption.fontSize = Mathf.Max(8, Mathf.RoundToInt(area.width * fontScale));
            GUI.Label(area, label, caption);
            GUI.color = previous;
        }

        internal bool DrawEditor()
        {
            if (Event.current.type == EventType.Repaint)
                for (int i = 0; i < sizes.Length; i++)
                    DrawButton(i, i == selected, false);

            float scale = Mathf.Min(Screen.width / 800f, Screen.height / 460f);
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float x = Screen.width / scale / 2 - 170;
            Rect box = new(x, 140, 340, 185);
            GUI.Box(box, L("Touch layout", "Controles por toque"));
            if (CostaUi.Button(new Rect(x + 20, 170, 42, 34), "‹", GUI.skin.button))
                selected = (selected + sizes.Length - 1) % sizes.Length;
            if (CostaUi.Button(new Rect(x + 278, 170, 42, 34), "›", GUI.skin.button))
                selected = (selected + 1) % sizes.Length;
            GUI.Label(new Rect(x + 105, 177, 130, 30), Title(selected));
            sizes[selected] = CostaUi.Slider(new Rect(x + 25, 220, 290, 24), sizes[selected], .065f, selected == 0 ? .34f : .18f);
            if (CostaUi.Button(new Rect(x + 25, 266, 130, 35), L("Reset this button", "Restaurar botão"), GUI.skin.button))
                sizes[selected] = DefaultSize(selected);
            bool done = CostaUi.Button(new Rect(x + 185, 266, 130, 35), L("Save and close", "Salvar e fechar"), GUI.skin.button);
            GUI.matrix = old;
            if (done)
            {
                for (int i = 0; i < sizes.Length; i++)
                    PlayerPrefs.SetFloat($"CostaTouchSize{i}", sizes[i]);
                PlayerPrefs.Save();
            }
            return done;
        }
    }
}
