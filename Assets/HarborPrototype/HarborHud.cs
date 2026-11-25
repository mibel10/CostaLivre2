using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class HarborHud
    {
        private static readonly Color Panel = new(0.059f, 0.078f, 0.079f, 0.87f);
        private static readonly Color Edge = new(0.37f, 0.42f, 0.40f, 0.72f);
        private static readonly Color Muted = new(0.63f, 0.67f, 0.63f);
        private static readonly Color Paper = new(0.91f, 0.90f, 0.83f);
        private static readonly Color Danger = new(0.91f, 0.45f, 0.35f);

        private readonly CostaMap map;
        private GUIStyle title;
        private GUIStyle small;
        private GUIStyle centered;
        private GUIStyle resultTitle;

        internal HarborHud(CostaMap map)
        {
            this.map = map;
        }

        internal void DrawRun(Vector3 player, float heading, Vector3? destination, Vector3 viewport, float side,
            string objective, float timer, bool pursuit, string notice, string prompt, float stamina, float speed,
            int health, HarborArsenal arsenal, bool touch, bool gamepad)
        {
            Matrix4x4 old = GUI.matrix;
            Begin(out float width, out float height);

            map.DrawMini(new Rect(18, 39, 132, 100), player, heading, destination);

            float statusX = touch ? 166 : width - 277;
            float statusY = 18;
            if (pursuit)
            {
                Chip(statusX, statusY, L("Police alerted", "Polícia alertada"), Danger, 239);
                statusY += 29;
            }
            if (objective.Length > 0 || timer >= 0)
            {
                string status = timer >= 0 ? $"{Mathf.CeilToInt(timer)}s   {objective}" : objective;
                Chip(statusX, statusY, status, Paper, 258);
            }

            float meterY = touch ? 146 : height - 53;
            if (speed >= 0)
            {
                Chip(18, meterY, $"{speed * 3.6f:0} km/h", Paper, 123);
                meterY += 27;
            }
            else if (stamina < 0.99f)
            {
                DrawMeter(18, meterY, L("Stamina", "Fôlego"), stamina, HarborBlockout.Mint);
                meterY += 27;
            }
            if (health >= 0 && health < 100)
                DrawMeter(18, meterY, L($"Health {health}", $"Vida {health}"), health / 100f, Danger);

            if (speed < 0)
            {
                DrawLoadout(width, height, arsenal, touch, gamepad);
                if (arsenal.HasFirearm)
                    DrawCrosshair(width / 2, height / 2, arsenal.HitVisible);
            }

            if (notice.Length > 0)
                CenterChip(width, touch ? 85 : 20, notice, HarborBlockout.Mint, 410);
            if (prompt.Length > 0)
                CenterChip(width, touch ? height * 0.60f : height - 38, prompt, Paper, width - 36);

            if (destination is Vector3 target)
            {
                bool offscreen = viewport.z <= 0 || viewport.x < 0.09f || viewport.x > 0.91f;
                float x = viewport.z <= 0 ? (side < 0 ? 174 : width - 174) : viewport.x * width;
                float y = viewport.z <= 0 ? height * .46f : (1 - viewport.y) * height;
                string arrow = offscreen ? (side < 0 ? "‹ " : "› ") : "";
                string distance = $"{arrow}{Mathf.CeilToInt(Vector3.Distance(player, target))} m";
                Chip(Mathf.Clamp(x, 174, width - 174) - TextWidth(distance, 12, 85) / 2,
                    Mathf.Clamp(y, 151, height * .55f), distance, HarborBlockout.Amber, 85);
            }
            GUI.matrix = old;
        }

        private void DrawMeter(float x, float y, string text, float progress, Color color)
        {
            Box(new Rect(x, y, 117, 24), Panel);
            Label(new Rect(x + 7, y, 103, 20), text, small, Paper);
            Box(new Rect(x + 7, y + 19, 103, 2), Edge);
            Box(new Rect(x + 7, y + 19, 103 * Mathf.Clamp01(progress), 2), color);
        }

        private float TextWidth(string text, float padding, float maxWidth) =>
            Mathf.Min(maxWidth, centered.CalcSize(new GUIContent(text)).x + padding);

        private void Chip(float x, float y, string text, Color color, float maxWidth)
        {
            Rect rect = new(x, y, TextWidth(text, 12, maxWidth), 24);
            Box(rect, Panel);
            Label(rect, text, centered, color);
        }

        private void CenterChip(float width, float y, string text, Color color, float maxWidth)
        {
            Chip((width - TextWidth(text, 12, maxWidth)) / 2, y, text, color, maxWidth);
        }

        private void DrawLoadout(float width, float height, HarborArsenal arsenal, bool touch, bool gamepad)
        {
            float slotWidth = touch ? 90 : 114;
            float start = touch ? 18 : width - 24 - slotWidth * 3;
            float slotsY = touch ? 249 : height - 59;
            for (int i = 0; i < 3; i++)
            {
                int weapon = i == 0 ? 0 : i == 1 ? arsenal.Heavy : HarborArsenal.Knife;
                bool selected = weapon >= 0 && weapon == arsenal.Slot;
                Rect slot = new(start + i * slotWidth, slotsY, slotWidth - 3, 25);
                Box(slot, selected ? new Color(0.28f, 0.32f, 0.27f, 0.96f) : Panel);
                bool owned = arsenal.Owned(weapon);
                Color edge = selected ? HarborBlockout.Amber : owned ? Edge : Color.clear;
                Box(new Rect(slot.x, slot.y + slot.height - 2, slot.width, 2), edge);
                string category = i == 0 ? L("Pistol", "Pistola") : L("Heavy", "Pesada");
                string slotText = owned ? Weapon(weapon) : category + " —";
                string label = touch || gamepad ? slotText : $"{i + 1} {slotText}";
                Label(slot, label, centered, selected ? Paper : Muted);
            }

            Rect card = new(touch ? start : width - 173, slotsY - 50, 146, 47);
            Box(card, Panel);
            if (!arsenal.Armed)
            {
                Label(new Rect(card.x + 8, card.y + 2, 130, 19), L("UNARMED", "DESARMADO"), small, Muted);
                Label(new Rect(card.x + 8, card.y + 18, 130, 25), L("Shop on map", "Armaria no mapa"), small, Paper);
                return;
            }

            Label(new Rect(card.x + 8, card.y + 2, 130, 19), Weapon(arsenal.Slot).ToUpperInvariant(), small, Muted);
            string loaded = arsenal.KnifeEquipped ? L("Melee", "Corpo a corpo")
                : arsenal.Reloading ? L("Reloading…", "Recarregando…") : $"{arsenal.Loaded:00} / {arsenal.Capacity:00}";
            Label(new Rect(card.x + 8, card.y + 18, 130, 25), loaded, title,
                arsenal.Loaded == 0 ? HarborBlockout.Amber : Paper);
        }

        private static void DrawCrosshair(float x, float y, bool hit)
        {
            Color color = hit ? Danger : Paper;
            Box(new Rect(x - 9, y - 1, 5, 2), color);
            Box(new Rect(x + 4, y - 1, 5, 2), color);
            Box(new Rect(x - 1, y - 9, 2, 5), color);
            Box(new Rect(x - 1, y + 4, 2, 5), color);
        }

        internal void DrawResult(string heading, string message, string retry, Color accent)
        {
            Matrix4x4 old = GUI.matrix;
            Begin(out float width, out float height);
            Box(new Rect(0, 0, width, height), new Color(0.03f, 0.045f, 0.046f, 0.71f));
            Rect card = new((width - 450) / 2, (height - 148) / 2, 450, 148);
            Box(card, Panel);
            Box(new Rect(card.x, card.y, card.width, 2), accent);
            Label(new Rect(card.x + 22, card.y + 13, 406, 58), heading, resultTitle, Paper);
            Label(new Rect(card.x + 22, card.y + 73, 406, 25), message, title, Muted);
            Label(new Rect(card.x + 22, card.y + 108, 406, 21), retry, small, accent);
            GUI.matrix = old;
        }

        private void Begin(out float width, out float height)
        {
            if (title == null)
            {
                title = MakeStyle(17, FontStyle.Bold, TextAnchor.MiddleLeft);
                small = MakeStyle(12, FontStyle.Bold, TextAnchor.MiddleLeft);
                centered = MakeStyle(12, FontStyle.Bold, TextAnchor.MiddleCenter);
                resultTitle = MakeStyle(37, FontStyle.Bold, TextAnchor.MiddleLeft);
            }

            float scale = Mathf.Min(Screen.width / 840f, Screen.height / 520f, 1.5f);
            width = Screen.width / scale;
            height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
        }

        private static GUIStyle MakeStyle(int size, FontStyle font, TextAnchor alignment)
        {
            return new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = font,
                alignment = alignment,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset()
            };
        }

        private static void Label(Rect rect, string content, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(rect, content, style);
        }

        private static void Box(Rect rect, Color color)
        {
            Color originalColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = originalColor;
        }
    }
}
