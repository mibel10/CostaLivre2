using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class HarborLoadingScreen
    {
        private static readonly Color Night = new(0.015f, 0.039f, 0.055f);
        private static readonly Color Paper = new(0.99f, 0.95f, 0.87f);
        private static readonly Color Muted = new(0.77f, 0.83f, 0.83f);
        private static readonly Color Amber = new(1f, 0.72f, 0.45f);
        private ResourceRequest imageRequest;
        private Texture2D fade;
        private GUIStyle title, label, percent, button;
        private float displayed;

        internal void Begin() => imageRequest = Resources.LoadAsync<Texture2D>("Harbor/SantaLuzLoading");

        internal void Draw(string phase, float progress, bool failed, Action retry)
        {
            if (title == null)
                CreateStyles();

            Rect screen = new(0, 0, Screen.width, Screen.height);
            Paint(screen, Night);
            if (imageRequest is { isDone: true, asset: Texture2D artwork })
                GUI.DrawTexture(screen, artwork, ScaleMode.ScaleAndCrop);
            Paint(screen, new Color(0.012f, 0.033f, 0.047f, 0.24f));
            GUI.DrawTexture(new Rect(0, Screen.height * 0.31f, Screen.width, Screen.height * 0.69f), fade);

            if (Event.current.type == EventType.Repaint)
                displayed = Mathf.MoveTowards(displayed, progress, Mathf.Max(Time.unscaledDeltaTime, 0.01f) * 0.84f);

            Rect safe = Screen.safeArea;
            if (safe.width <= 0 || safe.height <= 0)
                safe = screen;
            float scale = Mathf.Max(0.35f, Mathf.Min(safe.width / 500f, safe.height / 340f, 1.35f));
            Matrix4x4 matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(safe.x, Screen.height - safe.yMax, 0),
                Quaternion.identity, new Vector3(scale, scale, 1));
            float width = safe.width / scale;
            float height = safe.height / scale;
            const float margin = 27;
            float panelWidth = Mathf.Min(550, width - margin * 2);
            float top = height - 163;
            Paint(new Rect(margin, top, 39, 3), Amber);
            GUI.Label(new Rect(margin - 1, top + 9, panelWidth + 5, 67), "Santa Luz", title);
            GUI.Label(new Rect(margin, top + 78, panelWidth - 54, 41),
                failed ? L("Could not load the city.", "Não foi possível carregar a cidade.") : phase, label);
            if (failed)
            {
                Rect bounds = new(margin, top + 123, 168, 31);
                Paint(bounds, Amber);
                if (CostaUi.Button(bounds, L("Back to menu", "Voltar ao menu"), button))
                    retry();
            }
            else
            {
                float value = progress >= 1 ? 1 : displayed;
                GUI.Label(new Rect(margin + panelWidth - 54, top + 78, 54, 41), $"{Mathf.FloorToInt(value * 100)}%", percent);
                Paint(new Rect(margin, top + 127, panelWidth, 4), new Color(0.61f, 0.72f, 0.72f, 0.31f));
                if (value > 0)
                    Paint(new Rect(margin, top + 127, panelWidth * value, 4), Amber);
                float pulse = Mathf.PingPong(Time.unscaledTime * 0.75f, 1f);
                Paint(new Rect(margin + panelWidth * value * pulse, top + 126, 4, 6), Paper);
            }

            GUI.matrix = matrix;
        }

        internal void Release()
        {
            if (imageRequest is { isDone: true, asset: Texture2D artwork })
                Resources.UnloadAsset(artwork);
            UnityEngine.Object.Destroy(fade);
        }

        private void CreateStyles()
        {
            title = Style(58, Paper, TextAnchor.MiddleLeft);
            label = Style(16, Muted, TextAnchor.MiddleLeft);
            label.wordWrap = true;
            percent = Style(17, Amber, TextAnchor.MiddleRight);
            button = Style(15, Night, TextAnchor.MiddleCenter);
            fade = new Texture2D(1, 32, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            var colors = new Color[fade.height];
            for (int row = 0; row < fade.height; row++)
            {
                float strength = 1 - row / (fade.height - 1f);
                colors[row] = new Color(Night.r, Night.g, Night.b, Mathf.SmoothStep(0, 0.98f, strength));
            }
            fade.SetPixels(colors);
            fade.Apply(false, true);
        }

        private static GUIStyle Style(int size, Color color, TextAnchor alignment) => new(GUI.skin.label)
        {
            fontSize = size,
            fontStyle = FontStyle.Bold,
            alignment = alignment,
            padding = new RectOffset(),
            normal = { textColor = color }
        };

        private static void Paint(Rect bounds, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
