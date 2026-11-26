using UnityEngine;

namespace CostaLivre
{
    internal sealed class HarborDaylight
    {
        internal const float StartHour = 8f;
        internal const double CycleSeconds = 12 * 60;

        private static readonly int SkyTint = Shader.PropertyToID("_SkyTint");
        private static readonly int SkyGround = Shader.PropertyToID("_GroundColor");
        private static readonly int Exposure = Shader.PropertyToID("_Exposure");
        private static readonly int SunSize = Shader.PropertyToID("_SunSize");

        private readonly Light sun;
        private readonly Light originalSun = RenderSettings.sun;
        private readonly Material originalSkybox = RenderSettings.skybox;
        private readonly Color originalFog = RenderSettings.fogColor;
        private readonly float originalFogDensity = RenderSettings.fogDensity;
        private readonly float originalReflections = RenderSettings.reflectionIntensity;
        private readonly Color originalSky = RenderSettings.ambientSkyColor;
        private readonly Color originalHorizon = RenderSettings.ambientEquatorColor;
        private readonly Color originalGround = RenderSettings.ambientGroundColor;
        private readonly Quaternion originalRotation;
        private readonly Color originalColor;
        private readonly float originalIntensity;
        private readonly bool originalTemperature;
        private readonly LightShadows originalShadows;
        private Material skybox;
        private bool worldActive;

        internal HarborDaylight(Light sun)
        {
            this.sun = sun;
            if (sun != null)
            {
                originalRotation = sun.transform.rotation;
                originalColor = sun.color;
                originalIntensity = sun.intensity;
                originalTemperature = sun.useColorTemperature;
                originalShadows = sun.shadows;
            }
        }

        internal static float Advance(float hour, double seconds) =>
            Mathf.Repeat(hour + (float)(seconds % CycleSeconds * (24.0 / CycleSeconds)), 24f);

        internal static bool IsNight(float hour) => hour >= 19f || hour < 6f;

        internal static float SolarHeight(float hour) => Mathf.Sin((hour - 6f) * (Mathf.PI / 12f));

        internal void Apply(float? hour, bool ultra)
        {
            if (!hour.HasValue)
            {
                Restore();
                if (ultra)
                    ApplyUltraMenu();
                return;
            }

            if (!worldActive)
                BeginWorld();

            float time = hour.Value;
            float angle = (time - 6f) * (Mathf.PI / 12f);
            float height = SolarHeight(time);
            float skyLight = SmoothRange(-0.27f, 0.34f, height);
            float sunset = SmoothRange(-0.28f, 0.05f, height) * (1f - SmoothRange(0.07f, 0.62f, height));
            float moonlight = SmoothRange(0.01f, 0.36f, -height);
            Vector3 sunDirection = new Vector3(-Mathf.Cos(angle), height * 0.92f, 0.36f).normalized;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.LookRotation(height >= 0f ? -sunDirection : sunDirection);
                sun.color = height >= 0f
                    ? Color.Lerp(new Color(1f, 0.98f, 0.92f), new Color(1f, 0.57f, 0.31f), sunset)
                    : new Color(0.48f, 0.65f, 1f);
                sun.intensity = (ultra ? 1.62f : 1.27f) * SmoothRange(0f, 0.55f, height) + 0.19f * moonlight;
                LightShadows shadows = height >= 0f ? originalShadows : LightShadows.None;
                if (sun.shadows != shadows)
                    sun.shadows = shadows;
            }

            Color blueSky = ultra ? new Color(0.52f, 0.59f, 0.66f) : originalSky;
            Color horizon = ultra ? new Color(0.18f, 0.22f, 0.25f) : originalHorizon;
            Color ground = ultra ? new Color(0.091f, 0.078f, 0.064f) : originalGround;
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.085f, 0.13f, 0.22f),
                Color.Lerp(blueSky, new Color(0.56f, 0.37f, 0.31f), sunset * 0.78f), skyLight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.045f, 0.066f, 0.105f), horizon, skyLight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.031f, 0.042f, 0.058f), ground, skyLight);
            RenderSettings.fogColor = Color.Lerp(new Color(0.047f, 0.069f, 0.115f),
                Color.Lerp(new Color(0.58f, 0.69f, 0.75f), new Color(0.65f, 0.39f, 0.31f), sunset), skyLight);
            RenderSettings.fogDensity = Mathf.Lerp(0.0061f, ultra ? 0.0048f : originalFogDensity, skyLight);
            RenderSettings.reflectionIntensity = originalReflections * Mathf.Lerp(0.18f, 1f, skyLight);

            if (skybox != null)
            {
                SetColor(SkyTint, Color.Lerp(new Color(0.28f, 0.48f, 0.85f),
                    Color.Lerp(new Color(0.48f, 0.59f, 0.75f), new Color(0.91f, 0.57f, 0.44f), sunset), skyLight));
                SetColor(SkyGround, RenderSettings.fogColor);
                SetFloat(Exposure, Mathf.Lerp(0.25f, 1.13f, skyLight));
                SetFloat(SunSize, Mathf.Lerp(0.014f, 0.041f, skyLight));
            }
        }

        private void BeginWorld()
        {
            worldActive = true;
            if (sun != null)
            {
                RenderSettings.sun = sun;
                sun.useColorTemperature = false;
            }
            if (originalSkybox != null)
            {
                if (skybox == null)
                    skybox = new Material(originalSkybox);
                RenderSettings.skybox = skybox;
            }
        }

        private void ApplyUltraMenu()
        {
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(34f, -46f, 0);
                sun.color = new Color(1f, 0.94f, 0.83f);
                sun.intensity = 1.62f;
            }
            RenderSettings.fogColor = new Color(0.61f, 0.70f, 0.74f);
            RenderSettings.fogDensity = 0.0048f;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.59f, 0.66f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.22f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.091f, 0.078f, 0.064f);
        }

        private void Restore()
        {
            worldActive = false;
            RenderSettings.sun = originalSun;
            RenderSettings.skybox = originalSkybox;
            RenderSettings.fogColor = originalFog;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.reflectionIntensity = originalReflections;
            RenderSettings.ambientSkyColor = originalSky;
            RenderSettings.ambientEquatorColor = originalHorizon;
            RenderSettings.ambientGroundColor = originalGround;
            if (sun != null)
            {
                sun.transform.rotation = originalRotation;
                sun.color = originalColor;
                sun.intensity = originalIntensity;
                sun.useColorTemperature = originalTemperature;
                sun.shadows = originalShadows;
            }
        }

        internal void Release()
        {
            Restore();
            Object.Destroy(skybox);
        }

        private void SetColor(int property, Color value)
        {
            if (skybox.HasProperty(property))
                skybox.SetColor(property, value);
        }

        private void SetFloat(int property, float value)
        {
            if (skybox.HasProperty(property))
                skybox.SetFloat(property, value);
        }

        private static float SmoothRange(float lower, float upper, float value) =>
            Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(lower, upper, value));
    }
}
