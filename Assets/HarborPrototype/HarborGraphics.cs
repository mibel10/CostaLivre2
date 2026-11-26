using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CostaLivre
{
    internal sealed class HarborGraphics
    {
        internal const int MaxPreset = 4;
        internal const int MinDistance = 75;
        internal const int MaxDistance = 900;
        internal const int ScaleStep = 5;
        internal static int MinActualPercent => Mathf.RoundToInt(UniversalRenderPipeline.minRenderScale * 100);
        internal static bool FsrSupported => FSRUtils.IsSupported();

        private const string PresetKey = "CostaGraphicsPreset";
        private const string ShadowKey = "CostaShadowQuality";
        private const string TextureKey = "CostaTextureQuality";
        private const string ScaleKey = "CostaRenderPercent";
        private const string FsrKey = "CostaFsrEnabled";
        private const string DistanceKey = "CostaViewDistance";

        private static readonly int[] Distances = { 95, 155, 245, 360, 540 };
        private static readonly int[] Scales = { 50, 70, 85, 100, 100 };
        private static readonly int[] Textures = { 0, 1, 2, 3, 3 };
        private static readonly (int Distance, int Resolution, int Cascades)[] Shadows =
        {
            (0, 512, 1), (30, 1024, 1), (65, 2048, 2), (95, 2048, 4), (175, 4096, 4)
        };

        private readonly Camera camera;
        private readonly HarborDaylight daylight;
        private readonly Volume volume;
        private readonly VolumeProfile originalVolume;
        private float? worldHour;
        private RenderPipelineAsset originalPipeline;
        private UniversalRenderPipelineAsset pipeline;
        private double? saveAt;

        internal int Preset { get; private set; }
        internal int ShadowQuality { get; private set; }
        internal int TextureQuality { get; private set; }
        internal int ResolutionPercent { get; private set; }
        internal int Distance { get; private set; }
        internal bool FsrEnabled { get; private set; }
        internal bool IsCustom => ShadowQuality != Preset || TextureQuality != Textures[Preset] ||
            ResolutionPercent != Scales[Preset] || Distance != Distances[Preset] || FsrEnabled;
        internal int ActualPercent => Mathf.Max(ResolutionPercent, MinActualPercent);

        internal HarborGraphics(Camera camera, Light sun, Volume volume)
        {
            this.camera = camera;
            daylight = new HarborDaylight(sun);
            this.volume = volume;
            originalVolume = volume != null ? volume.sharedProfile : null;
            int oldDefault = PlayerPrefs.GetInt("CostaQuality", Application.isMobilePlatform ? 0 : 1) == 0 ? 1 : 3;
            int maxPreset = Mathf.Min(MaxPreset, QualitySettings.names.Length - 1);
            Preset = Mathf.Clamp(PlayerPrefs.GetInt(PresetKey, oldDefault), 0, maxPreset);
            ShadowQuality = Mathf.Clamp(PlayerPrefs.GetInt(ShadowKey, Preset), 0, MaxPreset);
            TextureQuality = Mathf.Clamp(PlayerPrefs.GetInt(TextureKey, Textures[Preset]), 0, 3);
            ResolutionPercent = RoundScale(PlayerPrefs.GetInt(ScaleKey, Scales[Preset]));
            Distance = Mathf.Clamp(PlayerPrefs.GetInt(DistanceKey, Distances[Preset]), MinDistance, MaxDistance);
            FsrEnabled = PlayerPrefs.GetInt(FsrKey, 0) != 0;
            SetupPipeline();
        }

        internal void SetPreset(int preset)
        {
            preset = Mathf.Clamp(preset, 0, Mathf.Min(MaxPreset, QualitySettings.names.Length - 1));
            if (preset == Preset && !IsCustom)
                return;
            bool switchAsset = Preset != preset;
            if (switchAsset)
                RestorePipeline();
            Preset = preset;
            ShadowQuality = preset;
            TextureQuality = Textures[preset];
            ResolutionPercent = Scales[preset];
            Distance = Distances[preset];
            FsrEnabled = false;
            if (switchAsset)
                SetupPipeline();
            else
                ApplySettings();
            QueueSave();
        }

        internal void SetShadows(int quality)
        {
            if (quality == ShadowQuality)
                return;
            ShadowQuality = quality;
            ApplyShadows();
            QueueSave();
        }

        internal void SetTextures(int quality)
        {
            if (quality == TextureQuality)
                return;
            TextureQuality = quality;
            ApplyTextures();
            QueueSave();
        }

        internal void SetScale(float value)
        {
            int percent = RoundScale(value);
            if (percent == ResolutionPercent)
                return;
            ResolutionPercent = percent;
            ApplyUpscaling();
            QueueSave();
        }

        internal void SetDistance(float value)
        {
            int distance = Mathf.RoundToInt(value / 5f) * 5;
            if (distance == Distance)
                return;
            Distance = distance;
            camera.farClipPlane = distance;
            QueueSave();
        }

        internal void ToggleFsr()
        {
            if (!FsrSupported)
                return;
            FsrEnabled = !FsrEnabled;
            ApplyUpscaling();
            QueueSave();
        }

        internal void SaveIfDue()
        {
            if (saveAt.HasValue && Time.realtimeSinceStartupAsDouble >= saveAt.Value)
                Flush();
        }

        internal void Flush()
        {
            if (!saveAt.HasValue)
                return;
            PlayerPrefs.Save();
            saveAt = null;
        }

        internal void SetWorldHour(float? hour)
        {
            if (worldHour == hour || (worldHour.HasValue && hour.HasValue && Mathf.Abs(worldHour.Value - hour.Value) < 0.0025f))
                return;
            worldHour = hour;
            daylight.Apply(hour, Preset == MaxPreset);
        }

        internal void Release()
        {
            Flush();
            if (volume != null)
                volume.sharedProfile = originalVolume;
            daylight.Release();
            RestorePipeline();
        }

        private void SetupPipeline()
        {
            if (QualitySettings.GetQualityLevel() != Preset)
                QualitySettings.SetQualityLevel(Preset, true);
            originalPipeline = QualitySettings.renderPipeline;
            if ((originalPipeline ?? GraphicsSettings.defaultRenderPipeline) is UniversalRenderPipelineAsset authored)
            {
                pipeline = Object.Instantiate(authored);
                pipeline.name = authored.name + " (runtime)";
                pipeline.hideFlags = HideFlags.DontSave;
            }
            ApplySettings();
            if (pipeline != null)
                QualitySettings.renderPipeline = pipeline;
        }

        private void RestorePipeline()
        {
            if (pipeline == null)
                return;
            if (QualitySettings.GetQualityLevel() == Preset && QualitySettings.renderPipeline == pipeline)
                QualitySettings.renderPipeline = originalPipeline;
            Object.Destroy(pipeline);
            pipeline = null;
        }

        private void ApplySettings()
        {
            ApplyShadows();
            ApplyTextures();
            ApplyUpscaling();
            bool ultra = Preset == MaxPreset;
            if (volume != null)
                volume.sharedProfile = ultra && pipeline?.volumeProfile != null ? pipeline.volumeProfile : originalVolume;
            daylight.Apply(worldHour, ultra);
            camera.farClipPlane = Distance;
        }

        private void ApplyShadows()
        {
            if (pipeline == null)
                return;
            var shadow = Shadows[ShadowQuality];
            pipeline.shadowDistance = shadow.Distance;
            pipeline.mainLightShadowmapResolution = shadow.Resolution;
            pipeline.shadowCascadeCount = shadow.Cascades;
        }

        private void ApplyTextures() => QualitySettings.globalTextureMipmapLimit = 3 - TextureQuality;

        private void ApplyUpscaling()
        {
            if (pipeline == null)
                return;
            pipeline.renderScale = ActualPercent / 100f;
            pipeline.upscalingFilter = FsrEnabled && FsrSupported && ActualPercent < 100
                ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Linear;
        }

        private static int RoundScale(float value) =>
            Mathf.RoundToInt(Mathf.Clamp(value, 0, 100) / ScaleStep) * ScaleStep;

        private void QueueSave()
        {
            PlayerPrefs.SetInt(PresetKey, Preset);
            PlayerPrefs.SetInt(ShadowKey, ShadowQuality);
            PlayerPrefs.SetInt(TextureKey, TextureQuality);
            PlayerPrefs.SetInt(ScaleKey, ResolutionPercent);
            PlayerPrefs.SetInt(FsrKey, FsrEnabled ? 1 : 0);
            PlayerPrefs.SetInt(DistanceKey, Distance);
            saveAt = Time.realtimeSinceStartupAsDouble + 0.5;
        }
    }
}
