using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class HarborHud
    {
        internal enum Mode { Full, Contextual, Minimal }
        private float mapVisibility = 1, kitVisibility, healthVisibility, armorVisibility, staminaVisibility, speedVisibility;
        private float equipmentUntil, displayedSpeed;
        private (int slot, int loaded) lastEquipment;

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
        private GUIStyle speedStyle;
        private readonly GUIContent measure = new();
        private readonly string[] slotLabels = new string[3];
        private (int Heavy, bool HeavyOwned, bool PistolOwned, bool Portuguese, bool Keys)? slotState;
        private (int Level, bool Portuguese) wantedState;
        private string wantedText;
        private (int Weapon, int Loaded, int Capacity, bool Reloading, bool Portuguese)? ammoState;
        private string weaponTitle, ammoText;

        internal HarborHud(CostaMap map)
        {
            this.map = map;
        }

        internal void DrawRun(Vector3 player, float heading, Vector3? destination, Vector3 viewport, float side,
            string objective, float timer, int wantedLevel, string notice, string prompt, float stamina, float arrest, float speed,
            int health, int armor, HarborArsenal arsenal, bool touch, bool gamepad, Mode mode, bool aiming)
        {
            Matrix4x4 old = GUI.matrix;
            Begin(out float width, out float height);

            Color color = GUI.color;
            bool driving = speed >= 0;
            bool full = mode == Mode.Full;
            bool contextual = mode == Mode.Contextual;
            bool danger = wantedLevel > 0 || arrest > 0 || aiming;
            float damping = 1 - Mathf.Exp(-Mathf.Min(Time.unscaledDeltaTime, .08f) * 15);
            var equipment = (arsenal.Slot, arsenal.Loaded);
            if (equipment != lastEquipment)
            {
                lastEquipment = equipment;
                equipmentUntil = Time.unscaledTime + 2.6f;
            }

            mapVisibility = Approach(mapVisibility, full || (contextual && (driving || destination.HasValue || danger)), damping);
            kitVisibility = Approach(kitVisibility, !driving && (full || aiming || arsenal.Reloading ||
                (contextual && (danger || Time.unscaledTime < equipmentUntil))), damping);
            staminaVisibility = Approach(staminaVisibility, !driving && (full || stamina < .97f), damping);
            healthVisibility = Approach(healthVisibility, health >= 0 && (full || health < 80 || (contextual && danger)), damping);
            armorVisibility = Approach(armorVisibility, armor >= 0 && (full || (contextual && (danger || armor < CostaSession.PoliceArmor))), damping);
            speedVisibility = Approach(speedVisibility, driving, damping);
            if (driving)
                displayedSpeed = speed;

            if (mapVisibility > 0)
            {
                GUI.color = Fade(color, mapVisibility);
                map.DrawMini(new Rect(18, 39 - (1 - mapVisibility) * 8, 152, 112), player, heading, destination);
                GUI.color = color;
            }

            float statusX = touch ? 186 : width - 277;
            float statusY = 18;
            if (wantedLevel > 0)
            {
                var state = (wantedLevel, Portuguese);
                if (wantedState != state)
                {
                    wantedState = state;
                    wantedText = new string('★', wantedLevel) + new string('☆', CostaSession.MaxWanted - wantedLevel)
                        + L("   WANTED", "   PROCURADO");
                }
                Chip(statusX, statusY, wantedText, Danger, 239);
                statusY += 29;
            }
            if (objective.Length > 0 || timer >= 0)
            {
                string status = timer >= 0 ? $"{Mathf.CeilToInt(timer)}s   {objective}" : objective;
                Chip(statusX, statusY, status, Paper, 258);
            }

            float stackHeight = 50 * speedVisibility + 27 * staminaVisibility + 29 * healthVisibility + 27 * armorVisibility;
            float meterY = touch ? 39 + 119 * mapVisibility : height - 24 - stackHeight;
            if (speedVisibility > 0)
            {
                GUI.color = Fade(color, speedVisibility);
                DrawSpeedometer(18, meterY, displayedSpeed);
                meterY += 50 * speedVisibility;
            }
            if (staminaVisibility > 0)
            {
                GUI.color = Fade(color, staminaVisibility);
                DrawMeter(18, meterY, L("Stamina", "Fôlego"), stamina, HarborBlockout.Mint);
                meterY += 27 * staminaVisibility;
            }
            if (healthVisibility > 0 && health >= 0)
            {
                GUI.color = Fade(color, healthVisibility);
                Color hpColor = health > 55 ? new Color(0.42f, 0.80f, 0.54f)
                    : health > 25 ? HarborBlockout.Amber : Danger;
                DrawMeter(18, meterY, L($"Health {health}", $"Vida {health}"), health / 100f, hpColor, 152, 26);
                meterY += 29 * healthVisibility;
            }
            if (armorVisibility > 0 && armor >= 0)
            {
                GUI.color = Fade(color, armorVisibility);
                DrawMeter(18, meterY, L($"Armor {armor}", $"Colete {armor}"),
                    armor / (float)CostaSession.PoliceArmor, HarborBlockout.Mint, 152, 24);
            }
            if (kitVisibility > 0)
            {
                GUI.color = Fade(color, kitVisibility);
                DrawLoadout(width, height + (1 - kitVisibility) * 8, arsenal, touch, gamepad);
            }
            GUI.color = color;
            if (!driving && arsenal.HasFirearm && (full || aiming || arsenal.HitVisible))
                DrawCrosshair(width / 2, height / 2, arsenal.HitVisible);

            if (notice.Length > 0)
                CenterChip(width, touch ? 85 : 20, notice, HarborBlockout.Mint, 410);
            if (prompt.Length > 0)
                CenterChip(width, touch ? height * 0.60f : height - 38, prompt, Paper, width - 36);

            if (arrest > 0)
            {
                float y = height * 0.67f;
                CenterChip(width, y, L("Arrest in progress", "Prisão em andamento"), Danger, 258);
                Box(new Rect(width / 2 - 62, y + 25, 124, 3), Edge);
                Box(new Rect(width / 2 - 62, y + 25, 124 * Mathf.Clamp01(arrest), 3), Danger);
            }

            if (destination is Vector3 target)
            {
                bool offscreen = viewport.z <= 0 || viewport.x < 0.09f || viewport.x > 0.91f;
                float x = viewport.z <= 0 ? (side < 0 ? 174 : width - 174) : viewport.x * width;
                float y = viewport.z <= 0 ? height * .46f : (1 - viewport.y) * height;
                string arrow = offscreen ? (side < 0 ? "‹ " : "› ") : "";
                string distance = $"{arrow}{Mathf.CeilToInt(Vector3.Distance(player, target))} m";
                float size = TextWidth(distance, 85);
                DrawChip(Mathf.Clamp(x, 174, width - 174) - size / 2, Mathf.Clamp(y, 151, height * .55f),
                    size, distance, HarborBlockout.Amber);
            }
            GUI.matrix = old;
        }

        private static float Approach(float value, bool visible, float damping)
        {
            float target = visible ? 1 : 0;
            float next = Mathf.Lerp(value, target, damping);
            return Mathf.Abs(target - next) < .005f ? target : next;
        }

        private static Color Fade(Color color, float visibility) =>
            new(color.r, color.g, color.b, color.a * visibility);

        private void DrawSpeedometer(float x, float y, float speed)
        {
            float kmh = speed * 3.6f;
            Box(new Rect(x, y, 152, 46), Panel);
            Label(new Rect(x + 10, y + 3, 86, 32), $"{kmh:0}", speedStyle, Paper);
            Label(new Rect(x + 96, y + 11, 48, 22), "km/h", small, Muted);
            Box(new Rect(x + 10, y + 38, 132, 4), Edge);
            Box(new Rect(x + 10, y + 38, 132 * Mathf.Clamp01(kmh / 160f), 4),
                kmh > 110f ? Danger : HarborBlockout.Amber);
        }

        private void DrawMeter(float x, float y, string text, float progress, Color color, float meterWidth = 117, float meterHeight = 24)
        {
            Box(new Rect(x, y, meterWidth, meterHeight), Panel);
            Label(new Rect(x + 7, y, meterWidth - 14, meterHeight - 4), text, small, Paper);
            Box(new Rect(x + 7, y + meterHeight - 5, meterWidth - 14, 3), Edge);
            Box(new Rect(x + 7, y + meterHeight - 5, (meterWidth - 14) * Mathf.Clamp01(progress), 3), color);
        }

        private float TextWidth(string text, float maxWidth)
        {
            measure.text = text;
            return Mathf.Min(maxWidth, centered.CalcSize(measure).x + 12);
        }

        private void Chip(float x, float y, string text, Color color, float maxWidth) =>
            DrawChip(x, y, TextWidth(text, maxWidth), text, color);

        private void DrawChip(float x, float y, float width, string text, Color color)
        {
            Rect rect = new(x, y, width, 24);
            Box(rect, Panel);
            Label(rect, text, centered, color);
        }

        private void CenterChip(float width, float y, string text, Color color, float maxWidth)
        {
            float size = TextWidth(text, maxWidth);
            DrawChip((width - size) / 2, y, size, text, color);
        }

        private void DrawLoadout(float width, float height, HarborArsenal arsenal, bool touch, bool gamepad)
        {
            float slotWidth = touch ? 90 : 114;
            float start = touch ? 18 : width - 24 - slotWidth * 3;
            float slotsY = touch ? 249 : height - 59;
            var equipment = (arsenal.Heavy, arsenal.Owned(arsenal.Heavy), arsenal.Owned(0), Portuguese, !touch && !gamepad);
            bool changed = slotState != equipment;
            slotState = equipment;
            for (int i = 0; i < 3; i++)
            {
                int weapon = i == 0 ? 0 : i == 1 ? arsenal.Heavy : HarborArsenal.Knife;
                bool selected = weapon >= 0 && weapon == arsenal.Slot;
                Rect slot = new(start + i * slotWidth, slotsY, slotWidth - 3, 25);
                Box(slot, selected ? new Color(0.28f, 0.32f, 0.27f, 0.96f) : Panel);
                bool owned = arsenal.Owned(weapon);
                Color edge = selected ? HarborBlockout.Amber : owned ? Edge : Color.clear;
                if (edge.a > 0)
                    Box(new Rect(slot.x, slot.y + slot.height - 2, slot.width, 2), edge);
                if (changed)
                {
                    string slotText = owned ? Weapon(weapon) : i == 0 ? L("Pistol —", "Pistola —") : L("Heavy —", "Pesada —");
                    slotLabels[i] = touch || gamepad ? slotText : $"{i + 1} {slotText}";
                }
                Label(slot, slotLabels[i], centered, selected ? Paper : Muted);
            }

            Rect card = new(touch ? start : width - 173, slotsY - 50, 146, 47);
            Box(card, Panel);
            if (!arsenal.Armed)
            {
                Label(new Rect(card.x + 8, card.y + 2, 130, 19), L("UNARMED", "DESARMADO"), small, Muted);
                Label(new Rect(card.x + 8, card.y + 18, 130, 25), L("Shop on map", "Armaria no mapa"), small, Paper);
                return;
            }

            var state = (arsenal.Slot, arsenal.Loaded, arsenal.Capacity, arsenal.Reloading, Portuguese);
            if (ammoState != state)
            {
                if (!ammoState.HasValue || ammoState.Value.Weapon != arsenal.Slot || ammoState.Value.Portuguese != Portuguese)
                    weaponTitle = Weapon(arsenal.Slot).ToUpperInvariant();
                ammoState = state;
                ammoText = arsenal.KnifeEquipped ? L("Melee", "Corpo a corpo")
                    : arsenal.Reloading ? L("Reloading…", "Recarregando…") : $"{arsenal.Loaded:00} / {arsenal.Capacity:00}";
            }
            Label(new Rect(card.x + 8, card.y + 2, 130, 19), weaponTitle, small, Muted);
            Label(new Rect(card.x + 8, card.y + 18, 130, 25), ammoText, title,
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
                speedStyle = MakeStyle(25, FontStyle.Bold, TextAnchor.MiddleLeft);
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
            GUI.color = originalColor * color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = originalColor;
        }
    }
}
