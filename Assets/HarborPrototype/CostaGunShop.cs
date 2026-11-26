using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaGunShop
    {
        private static readonly Color Dark = new(0.06f, 0.09f, 0.10f, 0.98f);
        private static readonly Color Paper = new(0.91f, 0.92f, 0.87f);
        private static readonly Color Accent = new(0.93f, 0.68f, 0.43f);
        private static readonly Color Metal = new(0.20f, 0.27f, 0.28f);

        private GUIStyle title;
        private GUIStyle label;
        private GUIStyle button;

        internal void Draw(int cash, HarborArsenal weapons, Func<int, bool> buy, Action<int> equip, Action close)
        {
            if (title == null)
            {
                title = MakeStyle(29);
                label = MakeStyle(15);
                button = MakeStyle(14);
                button.alignment = TextAnchor.MiddleCenter;
            }

            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 824f, Screen.height / 540f, 1.7f);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            Fill(new Rect(0, 0, width, height), new Color(0.03f, 0.05f, 0.06f, 0.72f));
            Rect box = new((width - 575) / 2, (height - 350) / 2, 575, 350);
            Fill(box, Dark);
            Fill(new Rect(box.x, box.y, box.width, 3), Accent);
            Write(new Rect(box.x + 22, box.y + 13, 440, 48), L("Santa Luz Gun Shop", "Armaria Santa Luz"), title, Paper);
            if (Button(new Rect(box.xMax - 95, box.y + 23, 73, 28), L("Close", "Fechar"), true))
                close();
            Write(new Rect(box.x + 22, box.y + 62, 480, 25), L("BALANCE  ", "SALDO  ") + Money(cash), label, Accent);

            for (int i = 0; i < CostaJobs.WeaponPrices.Length; i++)
            {
                float y = box.y + 99 + i * 44;
                Fill(new Rect(box.x + 20, y, box.width - 40, 36), Metal);
                Write(new Rect(box.x + 34, y, 178, 36), Weapon(i), label, Paper);
                bool owned = weapons.Owned(i);
                bool available = owned || cash >= CostaJobs.WeaponPrices[i];
                Write(new Rect(box.x + 209, y, 145, 36), owned ? L("Owned", "Comprada") : Money(CostaJobs.WeaponPrices[i]),
                    label, Paper);
                string action = i == weapons.Slot ? L("Equipped", "Equipada") : owned ? L("Equip", "Equipar") : L("Buy", "Comprar");
                if (Button(new Rect(box.xMax - 147, y + 4, 121, 28), action, available && weapons.Slot != i))
                {
                    if (owned || buy(i))
                        equip(i);
                }
            }

            GUI.matrix = previous;
        }

        private bool Button(Rect bounds, string name, bool active)
        {
            Fill(bounds, active ? Accent : Metal);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && active;
            button.normal.textColor = active ? Dark : Paper;
            bool pressed = CostaUi.Button(bounds, name, button);
            GUI.enabled = enabled;
            return pressed;
        }

        private static GUIStyle MakeStyle(int size) => new(GUI.skin.label)
        {
            fontSize = size, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Clip,
            alignment = TextAnchor.MiddleLeft, padding = new RectOffset()
        };

        private static void Write(Rect bounds, string value, GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            GUI.Label(bounds, value, style);
        }

        private static void Fill(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
