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
        private static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
        private static readonly int SunRadiance = Shader.PropertyToID("_SunRadiance");
        private static readonly int CloudCover = Shader.PropertyToID("_CloudCover");
        private static readonly int CloudDrift = Shader.PropertyToID("_CloudDrift");
        private static readonly int MistColor = Shader.PropertyToID("_CostaMistColor");
        private static readonly int Mist = Shader.PropertyToID("_CostaMist");
        private static readonly int SunSize = Shader.PropertyToID("_SunSize");

        private readonly Light sun;
        private readonly Light originalSun = RenderSettings.sun;
        private readonly Material originalSkybox = RenderSettings.skybox;
        private readonly Color originalFog = RenderSettings.fogColor;
        private readonly bool originalFogEnabled = RenderSettings.fog;
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
        private readonly float originalBias;
        private readonly float originalNormalBias;
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
                originalBias = sun.shadowBias;
                originalNormalBias = sun.shadowNormalBias;
            }
        }

        internal static float Advance(float hour, double seconds) =>
            Mathf.Repeat(hour + (float)(seconds % CycleSeconds * (24.0 / CycleSeconds)), 24f);

        internal static float CityNight(float hour)
        {
            float evening = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(17.7f, 20f, hour));
            float morning = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(5f, 7.2f, hour));
            return Mathf.Max(evening, morning);
        }

        internal static float SolarHeight(float hour) => Mathf.Sin((hour - 6f) * (Mathf.PI / 12f));

        internal void Apply(float? hour, int preset, HarborWeather.Conditions weather)
        {
            bool ultra = preset >= HarborGraphics.MaxPreset;
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
                sun.color = Color.Lerp(Color.Lerp(sun.color, new Color(0.77f, 0.85f, 0.94f),
                    weather.Clouds * 0.66f), new Color(0.79f, 0.87f, 1f), weather.Flash);
                float direct = 1.45f * SmoothRange(0f, 0.55f, height) + 0.10f * moonlight;
                sun.intensity = direct * Mathf.Lerp(1, 0.30f, weather.Clouds) + 3.0f * weather.Flash;
                LightShadows shadows = height >= 0f ? LightShadows.Soft : LightShadows.None;
                if (sun.shadows != shadows)
                    sun.shadows = shadows;
            }

            Color blueSky = Color.Lerp(new Color(0.56f, 0.65f, 0.73f), new Color(0.38f, 0.45f, 0.53f), weather.Clouds);
            Color horizon = Color.Lerp(new Color(0.30f, 0.34f, 0.37f), new Color(0.25f, 0.30f, 0.34f), weather.Clouds);
            Color ground = new Color(0.13f, 0.11f, 0.085f);
            RenderSettings.ambientSkyColor = Color.Lerp(new Color(0.080f, 0.12f, 0.20f),
                Color.Lerp(blueSky, new Color(0.57f, 0.40f, 0.34f), sunset * (1 - weather.Clouds)), skyLight);
            RenderSettings.ambientEquatorColor = Color.Lerp(new Color(0.043f, 0.063f, 0.095f), horizon, skyLight);
            RenderSettings.ambientGroundColor = Color.Lerp(new Color(0.023f, 0.033f, 0.049f), ground, skyLight);
            Color haze = Color.Lerp(new Color(0.57f, 0.66f, 0.71f), new Color(0.43f, 0.52f, 0.60f), weather.Clouds);
            haze = Color.Lerp(new Color(0.065f, 0.094f, 0.145f),
                Color.Lerp(haze, new Color(0.68f, 0.49f, 0.40f), sunset * (1 - weather.Clouds) * 0.7f), skyLight);
            RenderSettings.fogColor = haze;
            RenderSettings.fogDensity = 0.0038f + weather.Mist * 0.006f + weather.Rain * 0.0034f;
            bool volumetric = HarborAtmosphereFeature.Supported && preset >= 2;
            if (RenderSettings.fog == volumetric)
                RenderSettings.fog = !volumetric;
            Shader.SetGlobalColor(MistColor, haze);
            Shader.SetGlobalVector(Mist, new Vector4(volumetric ? 1 : 0, 0.0016f + 0.015f * weather.Mist +
                0.005f * weather.Rain, 0.045f, ultra ? 12 : 8));
            RenderSettings.reflectionIntensity = originalReflections * Mathf.Lerp(0.46f, 1f, skyLight);

            if (skybox != null)
            {
                SetColor(SkyTint, Color.Lerp(new Color(0.28f, 0.48f, 0.85f),
                    Color.Lerp(Color.Lerp(new Color(0.48f, 0.59f, 0.75f), new Color(0.48f, 0.52f, 0.57f), weather.Clouds),
                        new Color(0.91f, 0.66f, 0.51f), sunset * (1 - weather.Clouds)), skyLight));
                SetColor(SkyGround, RenderSettings.fogColor);
                SetColor(SunRadiance, sun != null ? sun.color * sun.intensity : Color.black);
                if (skybox.HasProperty(SunDirection))
                    skybox.SetVector(SunDirection, (Vector4)sunDirection);
                SetFloat(CloudCover, weather.Clouds);
                SetFloat(CloudDrift, time - 12f);
                SetFloat(Exposure, Mathf.Lerp(0.20f, 1.04f, skyLight) * Mathf.Lerp(1, 0.66f, weather.Clouds));
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
                sun.shadowBias = 0.035f;
                sun.shadowNormalBias = 0.23f;
            }
            if (skybox == null)
            {
                Shader shader = Resources.Load<Shader>("Harbor/CoastalSky");
                if (shader != null && shader.isSupported)
                    skybox = new Material(shader);
                else if (originalSkybox != null)
                    skybox = new Material(originalSkybox);
            }
            if (skybox != null)
                RenderSettings.skybox = skybox;
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
            Shader.SetGlobalVector(Mist, Vector4.zero);
            RenderSettings.fog = originalFogEnabled;
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
                sun.shadowBias = originalBias;
                sun.shadowNormalBias = originalNormalBias;
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
