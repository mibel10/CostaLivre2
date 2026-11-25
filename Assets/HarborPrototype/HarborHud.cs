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

        internal void DrawRun(bool marked, bool pursuit, float stamina, Vector3 viewport, float side, string prompt,
            HarborArsenal arsenal, bool gamepad, Vector3 player, Vector3 target, float heading, string objective,
            string markerName, int cash, float timer, string notice, float speed)
        {
            Matrix4x4 old = GUI.matrix;
            Begin(out float width, out float height);
            Color accent = pursuit ? HarborBlockout.Mint : HarborBlockout.Amber;
            map.DrawMini(new Rect(24, 42, 155, 133), player, heading, marked ? target : null);
            DrawHeading(width, heading);

            Rect task = new(width - 295, 36, 271, 72);
            Box(task, Panel);
            Box(new Rect(task.x, task.y, 2, task.height), pursuit ? Danger : accent);
            string sub = pursuit ? L("POLICE ALERTED", "POLÍCIA ALERTADA") : L("SANTA LUZ DISTRICT", "DISTRITO DE SANTA LUZ");
            Label(new Rect(task.x + 10, task.y + 3, 204, 20), sub, small, pursuit ? Danger : Muted);
            if (timer >= 0)
                Label(new Rect(task.x + 212, task.y + 3, 53, 20), $"{Mathf.CeilToInt(timer):00}s", small, accent);
            Label(new Rect(task.x + 10, task.y + 22, 260, 24), objective, small, Paper);
            Label(new Rect(task.x + 10, task.y + 45, 206, 21), Money(cash), small, accent);

            Rect endurance = new(24, height - 61, 135, 32);
            Box(endurance, Panel);
            if (speed < 0)
            {
                Label(new Rect(endurance.x + 8, endurance.y, 118, 19), L("STAMINA", "FÔLEGO"), small, Muted);
                Box(new Rect(endurance.x + 8, endurance.y + 22, 119, 3), Edge);
                Box(new Rect(endurance.x + 8, endurance.y + 22, 119 * stamina, 3), HarborBlockout.Mint);
                DrawLoadout(width, height, arsenal, gamepad);
            }
            else
            {
                Label(new Rect(endurance.x + 8, endurance.y, 118, 29), $"{speed * 3.6f:00} km/h", title, Paper);
            }

            if (notice.Length > 0)
            {
                Rect toast = new(width / 2 - 234, 51, 468, 31);
                Box(toast, Panel);
                Label(toast, notice, centered, HarborBlockout.Mint);
            }
            float promptWidth = Mathf.Min(width - 45, centered.CalcSize(new GUIContent(prompt)).x + 34);
            if (prompt.Length > 0)
            {
                Rect promptBox = new((width - promptWidth) / 2, height - 32, promptWidth, 23);
                Box(promptBox, Panel);
                Label(promptBox, prompt, centered, Paper);
            }

            if (marked)
            {
                float x = viewport.z <= 0 ? (side < 0 ? 108 : width - 108) : viewport.x * width;
                float y = viewport.z <= 0 ? height * 0.47f : (1 - viewport.y) * height;
                x = Mathf.Clamp(x, 108, width - 108);
                y = Mathf.Clamp(y, 188, height - 125);
                bool offside = viewport.z <= 0 || viewport.x < 0.1f || viewport.x > 0.9f;
                Rect marker = new(x - 99, y - 12, 198, 25);
                Box(marker, Panel);
                string arrow = offside ? (side < 0 ? "‹  " : "›  ") : "";
                float distance = Vector3.Distance(player, target);
                Label(marker, $"{arrow}{markerName}   {Mathf.CeilToInt(distance)} m", centered, accent);
            }
            if (speed < 0 && arsenal.Armed)
                DrawCrosshair(width / 2, height / 2, arsenal.HitVisible);
            GUI.matrix = old;
        }

        private void DrawHeading(float width, float heading)
        {
            int angle = Mathf.RoundToInt(heading) % 360;
            string[] points = Portuguese ? new[] { "N", "NE", "L", "SE", "S", "SO", "O", "NO" }
                : new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            string direction = points[Mathf.RoundToInt(heading / 45f) % points.Length];
            Rect compass = new(width * 0.5f - 52, 13, 104, 22);
            Box(compass, Panel);
            Label(compass, $"{direction}    {angle:000}°", centered, Paper);
        }

        private void DrawLoadout(float width, float height, HarborArsenal arsenal, bool gamepad)
        {
            const float slotWidth = 126;
            float start = width - 24 - slotWidth * 2;
            for (int i = 0; i < 2; i++)
            {
                int weapon = i == 0 ? 0 : arsenal.Heavy;
                bool selected = weapon >= 0 && weapon == arsenal.Slot;
                Rect slot = new(start + i * slotWidth, height - 59, slotWidth - 3, 25);
                Box(slot, selected ? new Color(0.28f, 0.32f, 0.27f, 0.96f) : Panel);
                bool owned = arsenal.Owned(weapon);
                Color edge = selected ? HarborBlockout.Amber : owned ? Edge : Color.clear;
                Box(new Rect(slot.x, slot.y + slot.height - 2, slot.width, 2), edge);
                string category = i == 0 ? L("Pistol", "Pistola") : L("Heavy", "Pesada");
                string slotText = owned ? Weapon(weapon) : category + " —";
                string label = gamepad ? slotText : $"{i + 1} {slotText}";
                Label(slot, label, centered, selected ? Paper : Muted);
            }

            Rect card = new(width - 173, height - 112, 146, 47);
            Box(card, Panel);
            if (!arsenal.Armed)
            {
                Label(new Rect(card.x + 8, card.y + 2, 130, 19), L("UNARMED", "DESARMADO"), small, Muted);
                Label(new Rect(card.x + 8, card.y + 18, 130, 25), L("Shop on map", "Armaria no mapa"), small, Paper);
                return;
            }

            Label(new Rect(card.x + 8, card.y + 2, 130, 19), Weapon(arsenal.Slot).ToUpperInvariant(), small, Muted);
            string loaded = arsenal.Reloading ? L("Reloading…", "Recarregando…") : $"{arsenal.Loaded:00} / {arsenal.Capacity:00}";
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
            Rect card = new((width - 450) / 2, (height - 186) / 2, 450, 186);
            Box(card, Panel);
            Box(new Rect(card.x, card.y, card.width, 2), accent);
            Label(new Rect(card.x + 22, card.y + 18, 406, 25), L("SANTA LUZ", "SANTA LUZ"), small, accent);
            Label(new Rect(card.x + 22, card.y + 41, 406, 58), heading, resultTitle, Paper);
            Label(new Rect(card.x + 22, card.y + 104, 406, 25), message, title, Muted);
            Label(new Rect(card.x + 22, card.y + 144, 406, 21), retry, small, accent);
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
