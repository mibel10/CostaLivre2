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

        internal void Draw(CostaBag bag, int cash, bool vendor, Action<int> sell, Action close)
        {
            if (heading == null)
            {
                heading = Style(28, TextAnchor.MiddleLeft);
                label = Style(15, TextAnchor.MiddleLeft);
                button = Style(14, TextAnchor.MiddleCenter);
            }
            Matrix4x4 before = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 840f, Screen.height / 560f, 1.6f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Box(new Rect(0, 0, width, height), new Color(0.03f, 0.06f, 0.07f, 0.74f));
            Rect bounds = new((width - 590) / 2, (height - 456) / 2, 590, 456);
            Box(bounds, Background);
            Box(new Rect(bounds.x, bounds.y, bounds.width, 3), Gold);
            Text(new Rect(bounds.x + 20, bounds.y + 12, 420, 38),
                vendor ? L("Loot exchange", "Posto de venda") : L("Backpack", "Mochila"), heading, Ink);
            if (Button(new Rect(bounds.xMax - 93, bounds.y + 16, 73, 27), L("Close", "Fechar"), true))
                close();

            Text(new Rect(bounds.x + 20, bounds.y + 55, 536, 29),
                $"{bag.Count} / {CostaBag.Capacity}  {L("spaces", "espaços")}   ·   {L("Cash", "Saldo")}  {Money(cash)}", label, Gold);
            if (bag.Count == 0)
                Text(new Rect(bounds.x + 25, bounds.y + 105, 510, 30), L("The backpack is empty.", "A mochila está vazia."), label, Detail);

            int visible = 0;
            for (int i = 0; i < CostaBag.Catalog.Length; i++)
            {
                int quantity = bag.Amounts[i];
                if (quantity == 0)
                    continue;
                var item = CostaBag.Catalog[i];
                float y = bounds.y + 94 + visible++ * 36;
                Box(new Rect(bounds.x + 20, y, bounds.width - 40, 31), Row);
                Text(new Rect(bounds.x + 31, y, 77, 31), $"{quantity}×", label, Gold);
                Text(new Rect(bounds.x + 73, y, 263, 31), item.Title, label, Ink);
                Text(new Rect(bounds.x + 328, y, 105, 31), Money(item.Price), label, Detail);
                if (vendor && Button(new Rect(bounds.xMax - 110, y + 3, 80, 25), L("Sell 1", "Vender 1"), true))
                    sell(i);
            }

            Text(new Rect(bounds.x + 20, bounds.yMax - 50, 334, 35),
                L("Total value  ", "Valor total  ") + Money(bag.Value), label, Ink);
            if (vendor && Button(new Rect(bounds.xMax - 176, bounds.yMax - 47, 156, 32), L("Sell all", "Vender tudo"), bag.Count > 0))
                sell(-1);
            GUI.matrix = before;
        }

        private bool Button(Rect area, string text, bool active)
        {
            Box(area, active ? Gold : Row);
            bool old = GUI.enabled;
            GUI.enabled = old && active;
            button.normal.textColor = Background;
            bool clicked = GUI.Button(area, text, button);
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
