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
        private const string AdaptiveKey = "CostaAdaptiveResolution";
        private const string DistanceKey = "CostaViewDistance";

        private static readonly int[] Distances = { 95, 155, 245, 360, 540 };
        private static readonly int[] Scales = { 50, 70, 85, 100, 100 };
        private static readonly int[] Textures = { 0, 1, 2, 3, 3 };
        private static readonly int[] StreetLights = { 0, 3, 5, 7, 10 };
        private static readonly (int Distance, int Resolution, int Cascades)[] Shadows =
        {
            (0, 512, 1), (30, 1024, 1), (65, 2048, 2), (95, 2048, 4), (175, 4096, 4)
        };

        private readonly Camera camera;
        private readonly UniversalAdditionalCameraData cameraData;
        private readonly AntialiasingMode originalAA;
        private readonly AntialiasingQuality originalAAQuality;
        private readonly bool originalMsaa;
        private readonly HarborDaylight daylight;
        private readonly Volume volume;
        private readonly VolumeProfile originalVolume;
        private float? worldHour;
        private HarborWeather.Conditions weather;
        private RenderPipelineAsset originalPipeline;
        private UniversalRenderPipelineAsset pipeline;
        private double? saveAt;
        private bool canMeasureGpu;
        private int renderPercent;
        private FrameTiming[] frameTimings;
        private float nextGpuCheck;
        private readonly CostaAdaptiveBudget budget = new();
        private readonly float[] observedFrames = new float[128];
        private readonly float[] sortedFrames = new float[128];
        private int observed;
        private int cursor;
        private int lastCollection;
        internal bool Extreme { get; private set; }
        internal bool ScaleLocked { get; private set; }
        internal bool WorldLocked { get; private set; }
        internal int WorldTier => WorldLocked ? 0 : budget.CpuTier;
        internal int PracticalTarget => Application.targetFrameRate is 30 or 60 or 90 or 120 ? Application.targetFrameRate : 60;
        internal float Frame95 { get; private set; }
        internal float LowOne { get; private set; }
        internal float MeanFrame { get; private set; }
        internal int ManagedCollections { get; private set; }
        internal double AllocatedMiB { get; private set; }
        internal double CpuMs { get; private set; }
        internal double GpuMs { get; private set; }
        internal string AdaptiveStatus => budget.Bottleneck.ToString();

        internal int Preset { get; private set; }
        internal int ShadowQuality { get; private set; }
        internal int TextureQuality { get; private set; }
        internal int ResolutionPercent { get; private set; }
        internal int Distance { get; private set; }
        internal bool FsrEnabled { get; private set; }
        internal bool AdaptiveResolution { get; private set; }
        internal bool AdaptiveSupported => pipeline != null;
        internal bool IsCustom => ShadowQuality != Preset || TextureQuality != Textures[Preset] ||
            ResolutionPercent != Scales[Preset] || Distance != Distances[Preset] || FsrEnabled;
        internal int StreetLightBudget => Mathf.Max(0,StreetLights[Preset]-(WorldLocked ? 0 : budget.GpuTier));
        internal int ActualPercent => renderPercent;

        internal HarborGraphics(Camera camera, Light sun, Volume volume)
        {
            this.camera = camera;
            cameraData = camera.GetUniversalAdditionalCameraData();
            originalAA = cameraData.antialiasing;
            originalAAQuality = cameraData.antialiasingQuality;
            originalMsaa = camera.allowMSAA;
            daylight = new HarborDaylight(sun);
            this.volume = volume;
            originalVolume = volume != null ? volume.sharedProfile : null;
            bool lowMemory = SystemInfo.systemMemorySize is > 0 and <= 6144;
            int defaultPreset = Application.isMobilePlatform || lowMemory ? 1 : 3;
            if (PlayerPrefs.HasKey("CostaQuality"))
                defaultPreset = PlayerPrefs.GetInt("CostaQuality") == 0 ? 1 : 3;
            int maxPreset = Mathf.Min(MaxPreset, QualitySettings.names.Length - 1);
            Preset = Mathf.Clamp(PlayerPrefs.GetInt(PresetKey, defaultPreset), 0, maxPreset);
            ShadowQuality = Mathf.Clamp(PlayerPrefs.GetInt(ShadowKey, Preset), 0, MaxPreset);
            TextureQuality = Mathf.Clamp(PlayerPrefs.GetInt(TextureKey, Textures[Preset]), 0, 3);
            ResolutionPercent = RoundScale(PlayerPrefs.GetInt(ScaleKey, Scales[Preset]));
            Distance = Mathf.Clamp(PlayerPrefs.GetInt(DistanceKey, Distances[Preset]), MinDistance, MaxDistance);
            FsrEnabled = PlayerPrefs.GetInt(FsrKey, 0) != 0;
            AdaptiveResolution = PlayerPrefs.GetInt(AdaptiveKey, 0) != 0;
            Extreme = PlayerPrefs.GetInt("CostaExtremePotato",0)!=0;
            ScaleLocked = PlayerPrefs.GetInt("CostaLockScale",0)!=0;
            WorldLocked = PlayerPrefs.GetInt("CostaLockWorld",0)!=0;
            SetupPipeline();
        }

        internal void SetPreset(int preset)
        {
            preset = Mathf.Clamp(preset, 0, Mathf.Min(MaxPreset, QualitySettings.names.Length - 1));
            if (preset == Preset && !IsCustom && !Extreme)
                return;
            Extreme=false;
            budget.Reset();
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
            ApplyUpscaling(renderPercent);
            QueueSave();
        }

        internal void ToggleAdaptiveResolution()
        {
            if (!AdaptiveSupported)
                return;
            AdaptiveResolution = !AdaptiveResolution;
            budget.Reset();
            ApplyDynamic();
            QueueSave();
        }

        internal void SetExtreme()
        {
            SetPreset(0);
            Extreme=true;
            ScaleLocked=WorldLocked=false;
            AdaptiveResolution=true;
            ShadowQuality=TextureQuality=0;
            ResolutionPercent=40;
            Distance=75;
            ApplySettings();
            QueueSave();
        }

        internal void ToggleScaleLock() { ScaleLocked=!ScaleLocked; ApplyDynamic(); QueueSave(); }
        internal void ToggleWorldLock() { WorldLocked=!WorldLocked; ApplyDynamic(); QueueSave(); }

        internal void UpdateAdaptiveResolution(bool inGameplay)
        {
            if (!inGameplay)
            {
                if (budget.Reset()) ApplyDynamic();
                observed=cursor=0;
                nextGpuCheck=0;
                return;
            }
            float ms=Time.unscaledDeltaTime*1000;
            if (ms>0)
            {
                observedFrames[cursor++ & 127]=ms;
                observed=Mathf.Min(observed+1,128);
            }
            if (canMeasureGpu) FrameTimingManager.CaptureFrameTimings();
            float now=Time.unscaledTime;
            if(now<nextGpuCheck || observed<12) return;
            nextGpuCheck=now+.55f;
            System.Array.Copy(observedFrames,sortedFrames,observed);
            System.Array.Sort(sortedFrames,0,observed);
            float total=0;
            for(int i=0;i<observed;i++) total+=sortedFrames[i];
            MeanFrame=total/observed;
            Frame95=sortedFrames[Mathf.CeilToInt(observed*.95f)-1];
            LowOne=1000f/Mathf.Max(.01f,sortedFrames[Mathf.CeilToInt(observed*.99f)-1]);
            int collections=System.GC.CollectionCount(0);
            ManagedCollections=collections-lastCollection;
            lastCollection=collections;
            long allocated=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            AllocatedMiB=allocated/1048576.0;
            frameTimings ??= new FrameTiming[8];
            uint count=canMeasureGpu ? FrameTimingManager.GetLatestTimings((uint)frameTimings.Length,frameTimings) : 0;
            double gpu=0,cpu=0;
            int gpuSamples=0,cpuSamples=0;
            for(int i=0;i<count;i++)
            {
                var timing=frameTimings[i];
                if(timing.gpuFrameTime>0){gpu+=timing.gpuFrameTime;gpuSamples++;}
                double busy=System.Math.Max(timing.cpuMainThreadFrameTime-timing.cpuMainThreadPresentWaitTime,timing.cpuRenderThreadFrameTime);
                if(busy>0){cpu+=busy;cpuSamples++;}
            }
            bool pressure=SystemInfo.systemMemorySize>0 && AllocatedMiB>SystemInfo.systemMemorySize*.56;
            CpuMs=cpu/System.Math.Max(1,cpuSamples);
            GpuMs=gpu/System.Math.Max(1,gpuSamples);
            if (AdaptiveResolution && budget.Sample(now,PracticalTarget,MeanFrame,CpuMs,GpuMs,pressure)) ApplyDynamic();
        }

        private void ApplyDynamic()
        {
            int ceiling=Mathf.Max(ResolutionPercent,MinActualPercent);
            int floor=Extreme ? Mathf.Max(MinActualPercent,25) : Mathf.Min(ceiling,Mathf.Max(MinActualPercent,45));
            int gpu=ScaleLocked ? 0 : budget.GpuTier;
            ApplyUpscaling(Mathf.Clamp(ceiling-gpu*5,Mathf.Min(floor,ceiling),ceiling));
            camera.farClipPlane=WorldLocked ? Distance : Mathf.Max(MinDistance,Distance-(budget.CpuTier+budget.GpuTier/3)*18);
            ApplyShadows();
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

        internal void SetWorldHour(float? hour, HarborWeather.Conditions conditions)
        {
            if (worldHour == hour || (worldHour.HasValue && hour.HasValue && Mathf.Abs(worldHour.Value - hour.Value) < 0.0025f))
                return;
            worldHour = hour;
            weather = conditions;
            daylight.Apply(hour, Preset, weather);
        }

        internal void Release()
        {
            Flush();
            if (volume != null)
                volume.sharedProfile = originalVolume;
            cameraData.antialiasing = originalAA;
            cameraData.antialiasingQuality = originalAAQuality;
            camera.allowMSAA = originalMsaa;
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
                if (pipeline.gpuResidentDrawerMode != GPUResidentDrawerMode.Disabled
                    && (Application.isMobilePlatform || !SystemInfo.supportsComputeShaders
                        || !IGPUResidentRenderPipeline.IsGPUResidentDrawerSupportedByProjectConfiguration()
                        || !pipeline.IsGPUResidentDrawerSupportedBySRP(out _, out _)))
                {
                    pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                    pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras = false;
                }
            }
            ApplySettings();
            if (pipeline != null)
                QualitySettings.renderPipeline = pipeline;
            canMeasureGpu = pipeline != null && FrameTimingManager.IsFeatureEnabled();
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
            daylight.Apply(worldHour, Preset, weather);
            camera.farClipPlane = Distance;
            camera.allowMSAA = Preset < 2 && originalMsaa;
            cameraData.antialiasing = Preset >= 2 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing :
                Preset == 1 ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            cameraData.antialiasingQuality = Preset >= 3 ? AntialiasingQuality.High : AntialiasingQuality.Medium;
        }

        private void ApplyShadows()
        {
            if (pipeline == null)
                return;
            var shadow = Shadows[Mathf.Max(0,ShadowQuality-(WorldLocked ? 0 : budget.GpuTier/3))];
            pipeline.shadowDistance = shadow.Distance;
            pipeline.mainLightShadowmapResolution = shadow.Resolution;
            pipeline.shadowCascadeCount = shadow.Cascades;
        }

        private void ApplyTextures() => QualitySettings.globalTextureMipmapLimit = 3 - TextureQuality;

        private void ApplyUpscaling() => ApplyUpscaling(Mathf.Max(ResolutionPercent, MinActualPercent));

        private void ApplyUpscaling(int percent)
        {
            renderPercent = percent;
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
            PlayerPrefs.SetInt("CostaExtremePotato",Extreme ? 1 : 0);
            PlayerPrefs.SetInt("CostaLockScale",ScaleLocked ? 1 : 0);
            PlayerPrefs.SetInt("CostaLockWorld",WorldLocked ? 1 : 0);
            PlayerPrefs.SetInt(PresetKey, Preset);
            PlayerPrefs.SetInt(ShadowKey, ShadowQuality);
            PlayerPrefs.SetInt(TextureKey, TextureQuality);
            PlayerPrefs.SetInt(ScaleKey, ResolutionPercent);
            PlayerPrefs.SetInt(FsrKey, FsrEnabled ? 1 : 0);
            PlayerPrefs.SetInt(AdaptiveKey, AdaptiveResolution ? 1 : 0);
            PlayerPrefs.SetInt(DistanceKey, Distance);
            saveAt = Time.realtimeSinceStartupAsDouble + 0.5;
        }
    }
}
