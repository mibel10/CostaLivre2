using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaBagPanel
    {
        private static readonly Color Background = new(0.056f, 0.075f, 0.078f, 0.97f);
        private static readonly Color Row = new(0.14f, 0.20f, 0.20f);
        private static readonly Color Ink = new(0.89f, 0.89f, 0.83f);
        private static readonly Color Detail = new(0.65f, 0.73f, 0.68f);
        private static readonly Color Gold = new(0.91f, 0.65f, 0.40f);
        private GUIStyle heading;
        private GUIStyle label;
        private GUIStyle button;

        private readonly CostaItemPreview preview;
        private readonly int[] order = { 0, 1, 2, 3, 4, 5, 6, 7 };
        private readonly Rect[] slots = new Rect[CostaBag.Catalog.Length];
        private int selected = -1;
        private int filter;
        private int drag = -1;
        private bool dragWithPointer;
        private Vector2 dragOrigin;
        private bool moved;
        private float shownAt;
        private float selectedAt;
        private float rotation;

        internal CostaBagPanel(HarborBlockout art, Transform root) => preview = new CostaItemPreview(art, root);

        internal void Open()
        {
            shownAt = Time.unscaledTime;
            drag = -1;
        }

        internal void Hide()
        {
            preview.Hide();
            drag = -1;
        }

        internal void Release() => preview.Release();

        private static int Category(int index) => index switch { 2 or 3 => 2, 6 or 7 => 3, _ => 1 };

        internal void Draw(CostaBag bag, int cash, bool vendor, Action<int> sell, Action close)
        {
            if (heading == null)
            {
                heading = Style(28, TextAnchor.MiddleLeft);
                label = Style(15, TextAnchor.MiddleLeft);
                button = Style(14, TextAnchor.MiddleCenter);
            }
            Matrix4x4 before = GUI.matrix;
            Rect screen = new(0, 0, Screen.width, Screen.height);
            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0)
                safe = screen;
            Box(screen, new Color(0.03f, 0.06f, 0.07f, 0.74f));
            float scale = Mathf.Min(safe.width / 840f, safe.height / 560f, 1.6f);
            float width = safe.width / scale;
            float height = safe.height / scale;
            GUI.matrix = before * Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));

            float opening = Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - shownAt) / .21f));
            Rect bounds = new((width - 700) * .5f, (height - 485) * .5f + (1-opening)*12, 700, 485);
            Box(bounds, Background);
            Box(new Rect(bounds.x, bounds.y, bounds.width, 3), Gold);
            Text(new Rect(bounds.x + 23, bounds.y + 9, 440, 42),
                vendor ? L("Loot exchange", "Posto de venda") : L("Backpack", "Mochila"), heading, Ink);
            if (Button(new Rect(bounds.xMax - 98, bounds.y + 15, 75, 29), L("Close", "Fechar"), true))
            {
                GUI.matrix = before;
                close();
                return;
            }
            Text(new Rect(bounds.x + 24, bounds.y + 51, 530, 27),
                $"{bag.Count} / {CostaBag.Capacity}  {L("spaces", "espaços")}   ·   {Money(cash)}", label, Gold);

            for (int i = 0; i < 4; i++)
            {
                string title = i switch
                {
                    0 => L("All", "Todos"), 1 => L("Valuables", "Valores"),
                    2 => L("Museum", "Museu"), _ => L("Electronics", "Eletrônicos")
                };
                Rect tab = new(bounds.x + 24 + i * 87, bounds.y + 81, 82, 29);
                if (Button(tab, title, true, filter == i))
                {
                    filter = i;
                    drag = -1;
                }
            }

            if (selected < 0 || bag.Amounts[selected] == 0 || filter != 0 && Category(selected) != filter)
            {
                selected = -1;
                foreach (int id in order)
                    if (bag.Amounts[id] > 0 && (filter == 0 || Category(id) == filter))
                    {
                        Select(id);
                        break;
                    }
            }

            int visible = 0;
            Array.Clear(slots, 0, slots.Length);
            foreach (int id in order)
            {
                int quantity = bag.Amounts[id];
                if (quantity <= 0 || filter != 0 && Category(id) != filter)
                    continue;
                float y = bounds.y + 120 + visible++ * 37;
                Rect slot = slots[id] = new(bounds.x + 24, y, 350, 34);
                Box(slot, id == selected ? Color.Lerp(Row, new Color(.35f,.29f,.21f), .3f +
                    .7f * Mathf.Clamp01((Time.unscaledTime - selectedAt) * 6)) : Row);
                if (id == selected)
                    Box(new Rect(slot.x, slot.y, 3, slot.height), Gold);
                Text(new Rect(slot.x + 13, y, 45, 34), $"{quantity}×", label, Gold);
                Text(new Rect(slot.x + 57, y, 200, 34), CostaBag.Catalog[id].Title, label, Ink);
                Text(new Rect(slot.xMax - 88, y, 85, 34), Money(CostaBag.Catalog[id].Price), label, Detail);
            }
            if (visible == 0)
                Text(new Rect(bounds.x + 25, bounds.y + 145, 349, 34), L("No items here.", "Nenhum item aqui."), label, Detail);

            Rect showcase = new(bounds.x + 391, bounds.y + 85, 283, 241);
            Box(showcase, Row);
            if (selected >= 0)
            {
                if (Event.current.type == EventType.Repaint)
                {
                    rotation = Mathf.Repeat(rotation + Time.unscaledDeltaTime * 24, 360);
                    Texture texture = preview.Render(selected, rotation);
                    GUI.DrawTexture(new Rect(showcase.center.x - 114, showcase.y, 228, 228), texture);
                }
                Text(new Rect(showcase.x + 13, showcase.yMax - 29, 259, 25), CostaBag.Catalog[selected].Title, label, Ink);
            }
            else
                preview.Hide();

            Rect drop = new(showcase.x, bounds.y + 338, showcase.width, 49);
            if (vendor)
            {
                Box(drop, drag >= 0 ? Color.Lerp(Row, Gold, .32f) : Row);
                Text(new Rect(drop.x+12, drop.y, drop.width-20, drop.height),
                    L("Drop an item here to sell", "Solte um item aqui para vender"), label, Ink);
                if (Button(new Rect(showcase.x, bounds.y + 402, 134, 32), L("Sell one", "Vender um"), selected >= 0))
                    sell(selected);
                if (Button(new Rect(showcase.x + 146, bounds.y + 402, 137, 32), L("Sell all", "Vender tudo"), bag.Count > 0))
                    sell(-1);
            }
            else
                Text(new Rect(showcase.x, bounds.y + 340, 279, 34), L("Drag to reorder", "Arraste para reorganizar"), label, Detail);
            Text(new Rect(showcase.x, bounds.yMax - 39, 279, 27),
                L("Total value  ", "Valor total  ") + Money(bag.Value), label, Gold);

            HandleDrag(bag, vendor, sell, drop);
            GUI.matrix = before;
        }

        private void HandleDrag(CostaBag bag, bool vendor, Action<int> sell, Rect drop)
        {
            Event input = Event.current;
            bool pointer = CostaUi.PointerActive;
            if (drag >= 0 && dragWithPointer != pointer)
                drag = -1;
            if (pointer && input.type != EventType.Repaint)
                return;
            Vector2 at = pointer ? CostaUi.Position : input.mousePosition;
            bool press = pointer ? CostaUi.PointerPressed : input.type == EventType.MouseDown && input.button == 0;
            bool release = pointer ? !CostaUi.PointerHeld : input.type == EventType.MouseUp && input.button == 0;

            if (press && drag < 0)
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].Contains(at))
                    {
                        drag = i;
                        dragWithPointer = pointer;
                        dragOrigin = at;
                        moved = false;
                        Select(i);
                        if (!pointer)
                            input.Use();
                        break;
                    }

            if (drag < 0)
                return;

            moved |= (at - dragOrigin).sqrMagnitude > 36;
            if (release)
            {
                if (moved && bag.Amounts[drag] > 0)
                {
                    if (vendor && drop.Contains(at))
                        sell(drag);
                    else
                        for (int i = 0; i < slots.Length; i++)
                            if (i != drag && slots[i].Contains(at))
                            {
                                int a = Array.IndexOf(order, drag), b = Array.IndexOf(order, i);
                                (order[a], order[b]) = (order[b], order[a]);
                                selectedAt = Time.unscaledTime;
                                break;
                            }
                }
                drag = -1;
                if (!pointer)
                    input.Use();
                return;
            }

            if (moved && input.type == EventType.Repaint)
            {
                Rect card = new(at.x + 8, at.y + 9, 207, 31);
                Box(card, new Color(Gold.r, Gold.g, Gold.b, .89f));
                Text(new Rect(card.x + 7, card.y, card.width - 14, card.height), CostaBag.Catalog[drag].Title, label, Background);
            }
        }

        private void Select(int index)
        {
            selected = index;
            selectedAt = Time.unscaledTime;
            rotation = 28;
        }

        private bool Button(Rect area, string text, bool active, bool selected = true)
        {
            Box(area, active && selected ? Gold : Row);
            bool old = GUI.enabled;
            GUI.enabled = old && active;
            button.normal.textColor = active && selected ? Background : Ink;
            bool clicked = CostaUi.Button(area, text, button);
            GUI.enabled = old;
            return clicked;
        }

        private static GUIStyle Style(int size, TextAnchor anchor) => new(GUI.skin.label)
        {
            fontSize = size, fontStyle = FontStyle.Bold, alignment = anchor, clipping = TextClipping.Clip,
            wordWrap = false, padding = new RectOffset()
        };

        private static void Text(Rect area, string text, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(area, text, style);
        }

        private static void Box(Rect area, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(area, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
