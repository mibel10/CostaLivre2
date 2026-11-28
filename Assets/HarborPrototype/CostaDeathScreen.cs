using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaDeathScreen
    {
        private const float SlowMotionScale = 0.18f;
        private const float SoloBannerStart = 0.38f;
        private const float SoloFadeToBlackStart = 2.65f;
        private const float SoloRespawnTime = 3.35f;
        private const float SoloTotalDuration = 4.25f;
        private const float OnlineTotalDuration = 2.65f;

        private bool active;
        private bool onlineMode;
        private bool respawnInvoked;
        private bool slowMotionApplied;
        private float elapsed;
        private float totalDuration;
        private string subtitle = "";
        private Action onRespawn;

        private Texture2D vignette;
        private GUIStyle titleStyle;
        private GUIStyle shadowStyle;
        private GUIStyle subtitleStyle;

        internal bool Active => active;
        internal bool BlocksGameplay => active && !onlineMode && !respawnInvoked;
        internal float Elapsed => elapsed;

        internal void BeginSolo(string detail, Action respawnCallback)
        {
            if (active && !onlineMode)
                return;

            active = true;
            onlineMode = false;
            respawnInvoked = false;
            elapsed = 0f;
            totalDuration = SoloTotalDuration;
            subtitle = detail ?? "";
            onRespawn = respawnCallback;
            Time.timeScale = SlowMotionScale;
            slowMotionApplied = true;
        }

        internal void BeginMultiplayer()
        {
            if (slowMotionApplied)
            {
                Time.timeScale = 1f;
                slowMotionApplied = false;
            }
            active = true;
            onlineMode = true;
            respawnInvoked = true;
            elapsed = 0f;
            totalDuration = OnlineTotalDuration;
            subtitle = L("Respawning in Santa Luz", "Retornando a Santa Luz");
            onRespawn = null;
        }

        internal void Cancel()
        {
            if (slowMotionApplied)
            {
                Time.timeScale = 1f;
                slowMotionApplied = false;
            }
            active = false;
            onlineMode = false;
            respawnInvoked = false;
            elapsed = 0f;
            onRespawn = null;
        }

        internal void Tick(bool paused)
        {
            if (!active || paused)
                return;

            elapsed += Mathf.Min(Time.unscaledDeltaTime, 0.08f);

            if (!onlineMode)
            {
                if (!respawnInvoked)
                {
                    float slowT = Mathf.Clamp01(elapsed / 0.35f);
                    Time.timeScale = Mathf.Lerp(0.45f, SlowMotionScale, slowT);
                    slowMotionApplied = true;

                    if (elapsed >= SoloRespawnTime)
                    {
                        respawnInvoked = true;
                        Time.timeScale = 1f;
                        slowMotionApplied = false;
                        Action callback = onRespawn;
                        onRespawn = null;
                        callback?.Invoke();
                    }
                }
            }

            if (elapsed >= totalDuration)
                Cancel();
        }

        internal void UpdateCameraAndBody(Camera camera, Transform playerRoot, Transform playerModel, float yaw, float pitch, bool inVehicle)
        {
            if (!BlocksGameplay || camera == null || playerRoot == null)
                return;

            float t = Mathf.Clamp01(elapsed / SoloRespawnTime);
            float collapse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.62f));

            if (playerModel != null && !inVehicle)
            {
                playerModel.localPosition = Vector3.down * (collapse * 0.62f);
                playerModel.rotation = Quaternion.Euler(collapse * 82f, yaw, collapse * 16f);
            }

            float pull = Mathf.Lerp(3.7f, 6.2f, Mathf.SmoothStep(0f, 1f, t));
            float lift = Mathf.Lerp(1.45f, 2.55f, t);
            float driftYaw = yaw + t * 14f;
            float dutch = Mathf.Sin(t * Mathf.PI * 0.65f) * 7.5f;

            Vector3 pivot = playerRoot.position + Vector3.up * 0.85f;
            Quaternion orbit = Quaternion.Euler(Mathf.Clamp(pitch + 14f * t, 6f, 44f), driftYaw, 0f);
            Vector3 desired = pivot + orbit * new Vector3(0f, lift - 0.85f, -pull);
            Vector3 dir = desired - pivot;
            float dist = dir.magnitude;
            if (dist > 0.05f && Physics.SphereCast(pivot, 0.18f, dir / dist, out RaycastHit hit, dist,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                desired = pivot + (dir / dist) * Mathf.Max(0.4f, hit.distance - 0.1f);
            }

            Quaternion look = Quaternion.LookRotation(pivot - desired, Vector3.up) * Quaternion.Euler(0f, 0f, dutch);
            camera.transform.SetPositionAndRotation(desired, look);
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, 52f, Mathf.Min(1f, Time.unscaledDeltaTime * 3.5f));
        }

        internal void Draw()
        {
            if (!active || Event.current.type != EventType.Repaint)
                return;

            float w = Screen.width;
            float h = Screen.height;
            Color previous = GUI.color;

            if (onlineMode)
                DrawOnline(w, h);
            else
                DrawSolo(w, h);

            GUI.color = previous;
        }

        private void DrawSolo(float w, float h)
        {
            // 1. Brief white/crimson impact flash
            float flash = Mathf.Clamp01(1f - elapsed / 0.28f);
            if (flash > 0.001f)
            {
                GUI.color = new Color(0.95f, 0.22f, 0.18f, flash * 0.42f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }

            // 2. Desaturated cold-dark cinematic wash + vignette while dying
            float bodyPresence = elapsed < SoloRespawnTime
                ? Mathf.Clamp01(elapsed / 0.45f)
                : Mathf.Clamp01(1f - (elapsed - SoloRespawnTime) / (SoloTotalDuration - SoloRespawnTime));

            if (bodyPresence > 0.001f)
            {
                GUI.color = new Color(0.06f, 0.08f, 0.10f, bodyPresence * 0.56f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);

                vignette ??= CreateVignette();
                GUI.color = new Color(0.02f, 0.02f, 0.03f, bodyPresence * 0.78f);
                GUI.DrawTexture(new Rect(0, 0, w, h), vignette);
            }

            // 3. GTA V center horizontal band + "WASTED" / "MORTO"
            if (elapsed >= SoloBannerStart && elapsed < SoloRespawnTime + 0.15f)
            {
                float bandIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - SoloBannerStart) / 0.28f));
                float bandOut = elapsed > SoloFadeToBlackStart
                    ? 1f - Mathf.Clamp01((elapsed - SoloFadeToBlackStart) / (SoloRespawnTime - SoloFadeToBlackStart))
                    : 1f;
                float alpha = bandIn * bandOut;
                DrawBanner(w, h, alpha, bandIn, elapsed >= SoloBannerStart + 0.45f ? Mathf.Clamp01((elapsed - (SoloBannerStart + 0.45f)) / 0.35f) * bandOut : 0f);
            }

            // 4. Fade to black -> respawn at hospital -> fade back into world
            float blackout = 0f;
            if (elapsed >= SoloFadeToBlackStart && elapsed < SoloRespawnTime)
                blackout = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SoloFadeToBlackStart, SoloRespawnTime, elapsed));
            else if (elapsed >= SoloRespawnTime)
                blackout = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(SoloRespawnTime, SoloTotalDuration, elapsed));

            if (blackout > 0.001f)
            {
                GUI.color = new Color(0f, 0f, 0f, blackout);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }
        }

        private void DrawOnline(float w, float h)
        {
            float fadeIn = Mathf.Clamp01(elapsed / 0.22f);
            float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OnlineTotalDuration - 0.65f, OnlineTotalDuration, elapsed));
            float alpha = fadeIn * fadeOut;

            float flash = Mathf.Clamp01(1f - elapsed / 0.22f);
            if (flash > 0.001f)
            {
                GUI.color = new Color(0.95f, 0.22f, 0.18f, flash * 0.35f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            }

            if (alpha > 0.001f)
            {
                GUI.color = new Color(0.05f, 0.07f, 0.09f, alpha * 0.45f);
                GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);

                vignette ??= CreateVignette();
                GUI.color = new Color(0.02f, 0.02f, 0.03f, alpha * 0.68f);
                GUI.DrawTexture(new Rect(0, 0, w, h), vignette);

                float subAlpha = Mathf.Clamp01((elapsed - 0.35f) / 0.25f) * fadeOut;
                DrawBanner(w, h, alpha, fadeIn, subAlpha);
            }
        }

        private void DrawBanner(float w, float h, float alpha, float enterT, float subAlpha)
        {
            if (alpha <= 0.001f)
                return;

            EnsureStyles(h);

            float bandHeight = Mathf.Clamp(h * 0.20f, 96f, 210f) * Mathf.Lerp(0.72f, 1f, enterT);
            float bandY = (h - bandHeight) * 0.5f;

            // Dark translucent GTA-style horizontal bar
            GUI.color = new Color(0.04f, 0.045f, 0.055f, alpha * 0.80f);
            GUI.DrawTexture(new Rect(0f, bandY, w, bandHeight), Texture2D.whiteTexture);

            // Thin crimson accent borders
            float lineHeight = Mathf.Max(2f, h * 0.003f);
            GUI.color = new Color(0.86f, 0.16f, 0.15f, alpha * 0.72f);
            GUI.DrawTexture(new Rect(0f, bandY, w, lineHeight), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, bandY + bandHeight - lineHeight, w, lineHeight), Texture2D.whiteTexture);

            string title = L("WASTED", "MORTO");
            float pop = Mathf.Lerp(1.16f, 1f, enterT);
            Vector2 center = new(w * 0.5f, bandY + bandHeight * (subtitle.Length > 0 ? 0.42f : 0.50f));
            Matrix4x4 previousMatrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(pop, pop), center);

            Rect titleRect = new(0f, center.y - bandHeight * 0.36f, w, bandHeight * 0.72f);
            float drop = Mathf.Max(3f, h * 0.0045f);
            GUI.color = new Color(0f, 0f, 0f, alpha * 0.90f);
            GUI.Label(new Rect(titleRect.x + drop, titleRect.y + drop, titleRect.width, titleRect.height), title, shadowStyle);

            GUI.color = new Color(0.92f, 0.19f, 0.17f, alpha);
            GUI.Label(titleRect, title, titleStyle);
            GUI.matrix = previousMatrix;

            if (subtitle.Length > 0 && subAlpha > 0.001f)
            {
                Rect subRect = new(w * 0.08f, bandY + bandHeight * 0.67f, w * 0.84f, bandHeight * 0.28f);
                GUI.color = new Color(0f, 0f, 0f, subAlpha * 0.75f);
                GUI.Label(new Rect(subRect.x + 1.5f, subRect.y + 1.5f, subRect.width, subRect.height), subtitle, subtitleStyle);
                GUI.color = new Color(0.93f, 0.93f, 0.92f, subAlpha * 0.95f);
                GUI.Label(subRect, subtitle, subtitleStyle);
            }
        }

        private void EnsureStyles(float screenHeight)
        {
            int titleSize = Mathf.Clamp(Mathf.RoundToInt(screenHeight * 0.092f), 34, 108);
            int subSize = Mathf.Clamp(Mathf.RoundToInt(screenHeight * 0.024f), 13, 28);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                titleStyle.normal.textColor = Color.white;
            }
            titleStyle.fontSize = titleSize;

            if (shadowStyle == null)
            {
                shadowStyle = new GUIStyle(titleStyle);
                shadowStyle.normal.textColor = Color.black;
            }
            shadowStyle.fontSize = titleSize;

            if (subtitleStyle == null)
            {
                subtitleStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                subtitleStyle.normal.textColor = Color.white;
            }
            subtitleStyle.fontSize = subSize;
        }

        private static Texture2D CreateVignette()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Death vignette",
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float ny = (y / (float)(size - 1)) * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x / (float)(size - 1)) * 2f - 1f;
                    float radial = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.SmoothStep(0.32f, 1.38f, radial);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        internal void Release()
        {
            Cancel();
            if (vignette != null)
            {
                UnityEngine.Object.Destroy(vignette);
                vignette = null;
            }
        }
    }
}
