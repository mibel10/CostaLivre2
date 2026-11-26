using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaTouch
    {
        internal enum Control { Move, Sprint, Crouch, Jump, Roll, Use, Aim, Fire, Reload, Swap, Map, Bag, Menu, Retry, Shoulder }
        private static readonly string[] English = { "Move", "Run", "Crouch", "Jump", "Roll", "Use", "Aim", "Fire", "Reload", "Swap", "Map", "Bag", "Menu", "Retry", "Shoulder" };
        private static readonly string[] PortugueseNames = { "Mover", "Correr", "Agachar", "Pular", "Rolar", "Usar", "Mirar", "Atirar", "Recarregar", "Trocar", "Mapa", "Mochila", "Menu", "Repetir", "Ombro" };
        private static readonly Vector2[] Centers = {
            new(.13f,.76f), new(.24f,.57f), new(.66f,.88f), new(.89f,.77f), new(.77f,.80f),
            new(.91f,.56f), new(.80f,.61f), new(.92f,.39f), new(.72f,.70f), new(.58f,.87f),
            new(.76f,.13f), new(.85f,.13f), new(.94f,.13f), new(.50f,.77f), new(.65f,.13f)
        };
        private readonly float[] sizes = new float[Centers.Length];
        private readonly bool[] down = new bool[Centers.Length];
        private readonly bool[] pressed = new bool[Centers.Length];
        private readonly int[] fingers = new int[Centers.Length];
        private int lookFinger = -1;
        private int selected;
        private readonly CostaTouchIcons icons = new();
        private Vector2 stickOffset;
        internal Vector2 Move { get; private set; }
        internal Vector2 Look { get; private set; }
        internal static bool Available => Application.isMobilePlatform && Touchscreen.current != null;

        internal CostaTouch()
        {
            for (int i = 0; i < sizes.Length; i++)
                sizes[i] = Mathf.Clamp(PlayerPrefs.GetFloat($"CostaTouchSize{i}", DefaultSize(i)), .065f, i == 0 ? .34f : .18f);
            ResetInput();
        }

        private Rect layoutArea;

        private void FitLayout()
        {
            Rect safe = Screen.safeArea;
            if (safe.Equals(layoutArea))
                return;
            layoutArea = safe;
            for (int i = 0; i < sizes.Length; i++)
                SetSize(i, sizes[i]);
        }

        private void SetSize(int index, float requested)
        {
            float low = .065f;
            float high = requested;
            for (int pass = 0; pass < 12; pass++)
            {
                float size = pass == 0 ? high : (low + high) * .5f;
                Rect area = Bounds(index, size);
                bool overlaps = false;
                for (int i = 0; i < sizes.Length; i++)
                {
                    if (i == index || (i == (int)Control.Retry || index == (int)Control.Retry)
                        && i != (int)Control.Menu && index != (int)Control.Menu)
                        continue;
                    if (area.Overlaps(Bounds(i)))
                    {
                        overlaps = true;
                        break;
                    }
                }
                if (overlaps)
                    high = size;
                else if (pass == 0)
                {
                    sizes[index] = size;
                    return;
                }
                else
                    low = size;
            }
            sizes[index] = low;
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
            Move = Look = stickOffset = Vector2.zero;
        }

        internal void Update(bool gameplay, bool driving, bool retry)
        {
            if (!Available || !gameplay)
            {
                ResetInput();
                return;
            }

            FitLayout();
            Array.Clear(pressed, 0, pressed.Length);
            Array.Clear(down, 0, down.Length);
            Move = Look = stickOffset = Vector2.zero;
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
                        float travel = area.width * .32f;
                        stickOffset = Vector2.ClampMagnitude(p - area.center, travel);
                        Vector2 axes = new Vector2(stickOffset.x, -stickOffset.y) / travel;
                        float magnitude = Mathf.InverseLerp(0.12f, 1f, axes.magnitude);
                        Move = axes.normalized * magnitude;
                    }
                }
                if (lookFinger == id)
                {
                    Look = finger.delta.ReadValue();
                    keepLook = true;
                }
            }
            for (int i = 0; i < fingers.Length; i++)
                if (!down[i])
                    fingers[i] = -1;
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

        private Rect Bounds(int i) => Bounds(i, sizes[i]);

        private Rect Bounds(int i, float relativeSize)
        {
            Rect safe = layoutArea;
            float size = Mathf.Min(safe.width, safe.height) * relativeSize;
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
            Rect area = Bounds(i);
            Color previous = GUI.color;
            GUI.color = highlighted ? new Color(0.72f, 0.40f, 0.16f, 0.86f) : new Color(0.045f, 0.08f, 0.11f, 0.68f);
            GUI.DrawTexture(area, icons.Round);
            GUI.color = highlighted ? new Color(1f, 0.76f, 0.38f) : new Color(0.73f, 0.86f, 0.91f, 0.74f);
            GUI.DrawTexture(area, icons.Rim);

            if (i == (int)Control.Move)
            {
                float thumbSize = area.width * 0.36f;
                Vector2 center = area.center + stickOffset;
                Rect thumb = new(center.x - thumbSize * 0.5f, center.y - thumbSize * 0.5f, thumbSize, thumbSize);
                GUI.DrawTexture(thumb, icons.Round);
            }
            else
            {
                GUI.color = new Color(0.97f, 0.97f, 0.91f);
                GUI.DrawTexture(area, icons.Icon(driving && i == (int)Control.Jump ? 15 : i));
            }
            GUI.color = previous;
        }

        internal void Release() => icons.Release();

        internal bool DrawEditor()
        {
            FitLayout();
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
            float requested = CostaUi.Slider(new Rect(x + 25, 220, 290, 24), sizes[selected], .065f, selected == 0 ? .34f : .18f);
            if (requested != sizes[selected])
                SetSize(selected, requested);
            if (CostaUi.Button(new Rect(x + 25, 266, 130, 35), L("Reset this button", "Restaurar botão"), GUI.skin.button))
                SetSize(selected, DefaultSize(selected));
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
