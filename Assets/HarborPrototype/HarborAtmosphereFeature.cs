using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CostaLivre
{
    public sealed class HarborAtmosphereFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader atmosphereShader;
        [SerializeField] private bool highQuality;
        private Material material;
        private AtmospherePass pass;

        internal static bool Supported => SystemInfo.graphicsShaderLevel >= 35 &&
            SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render) &&
            GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>()?.enableRenderCompatibilityMode != true;

        public override void Create()
        {
            Dispose(true);
            if (atmosphereShader == null || !atmosphereShader.isSupported || !Supported)
                return;

            material = CoreUtils.CreateEngineMaterial(atmosphereShader);
            pass = new AtmospherePass(material, highQuality);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (pass != null && camera.cameraType == CameraType.Game && !camera.stereoEnabled &&
                camera.CompareTag("MainCamera") && renderingData.cameraData.renderType == CameraRenderType.Base &&
                renderingData.cameraData.postProcessEnabled)
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            pass = null;
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class AtmospherePass : ScriptableRenderPass
        {
            private static readonly Vector4 Fullscreen = new Vector4(1, 1, 0, 0);
            private static readonly int History = Shader.PropertyToID("_CostaExposureHistory");
            private static readonly int Meter = Shader.PropertyToID("_CostaExposure");
            private static readonly int Scattering = Shader.PropertyToID("_CostaScattering");
            private static readonly int MeterParams = Shader.PropertyToID("_CostaMeterParams");
            private static readonly int Mist = Shader.PropertyToID("_CostaMist");
            private readonly Material material;
            private readonly bool highQuality;
            private readonly RTHandle[] history = new RTHandle[2];
            private int writeIndex;
            private int lastFrame = -2;
            private int lastCamera;
            private bool lastWorld;

            private sealed class PassData
            {
                internal TextureHandle Source, Extra, Meter;
                internal Material Material;
                internal int ShaderPass, ExtraId;
                internal Vector4 Parameters;
            }

            internal AtmospherePass(Material material, bool highQuality)
            {
                this.material = material;
                this.highQuality = highQuality;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
                ConfigureInput(ScriptableRenderPassInput.Depth);
                for (int i = 0; i < history.Length; i++)
                    history[i] = RTHandles.Alloc(1, 1, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                        filterMode: FilterMode.Point, name: "Costa exposure history " + i);
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid())
                    return;

                var camera = frameData.Get<UniversalCameraData>().camera;
                TextureHandle color = resources.activeColorTexture;
                var desc = graph.GetTextureDesc(color);
                desc.msaaSamples = MSAASamples.None;
                desc.bindTextureMS = false;
                desc.depthBufferBits = DepthBits.None;
                desc.clearBuffer = false;
                desc.name = "Costa exposed atmosphere";
                TextureHandle output = graph.CreateTexture(desc);

                bool world = Shader.GetGlobalVector(Mist).x > 0;
                TextureHandle previous = graph.ImportTexture(history[1 - writeIndex]);
                TextureHandle current = graph.ImportTexture(history[writeIndex]);
                writeIndex = 1 - writeIndex;
                bool continuing = lastCamera == camera.GetInstanceID() && lastWorld == world &&
                    lastFrame == Time.frameCount - 1;
                lastFrame = Time.frameCount;
                lastWorld = world;
                lastCamera = camera.GetInstanceID();

                Draw(graph, "Costa scene luminance", 0, color, current, previous, History,
                    parameters: new Vector4(Mathf.Min(Time.unscaledDeltaTime, 0.1f), continuing ? 1 : 0, 0, 0));

                TextureHandle scattering = TextureHandle.nullHandle;
                if (world)
                {
                    desc.name = "Costa half resolution scattering";
                    desc.width = Mathf.Max(1, (desc.width + 1) / 2);
                    desc.height = Mathf.Max(1, (desc.height + 1) / 2);
                    desc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                    desc.filterMode = FilterMode.Bilinear;
                    scattering = graph.CreateTexture(desc);
                    Draw(graph, "Costa light transport", 1, color, scattering, depth: resources.cameraDepthTexture,
                        shadows: resources.mainShadowsTexture, parameters: new Vector4(highQuality ? 1 : 0, 0, 0, 0));
                }

                Draw(graph, "Costa atmosphere composite", 2, color, output, scattering, Scattering, current,
                    depth: world ? resources.cameraDepthTexture : TextureHandle.nullHandle,
                    parameters: new Vector4(world ? 1 : 0, 0, 0, 0));
                resources.cameraColor = output;
            }

            private void Draw(RenderGraph graph, string name, int shaderPass, TextureHandle source,
                TextureHandle target, TextureHandle extra = default, int extraId = 0, TextureHandle meter = default,
                TextureHandle depth = default, TextureHandle shadows = default, Vector4 parameters = default)
            {
                using var builder = graph.AddRasterRenderPass<PassData>(name, out var data);
                data.Source = source;
                data.Extra = extra;
                data.ExtraId = extraId;
                data.Meter = meter;
                data.Material = material;
                data.ShaderPass = shaderPass;
                data.Parameters = parameters;
                builder.UseTexture(source);
                if (extra.IsValid())
                    builder.UseTexture(extra);
                if (meter.IsValid())
                    builder.UseTexture(meter);
                if (depth.IsValid())
                    builder.UseTexture(depth);
                if (shadows.IsValid())
                    builder.UseTexture(shadows);
                builder.SetRenderAttachment(target, 0, AccessFlags.WriteAll);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    if (pass.Extra.IsValid())
                        context.cmd.SetGlobalTexture(pass.ExtraId, pass.Extra);
                    if (pass.Meter.IsValid())
                        context.cmd.SetGlobalTexture(Meter, pass.Meter);
                    context.cmd.SetGlobalVector(MeterParams, pass.Parameters);
                    Blitter.BlitTexture(context.cmd, pass.Source, Fullscreen, pass.Material, pass.ShaderPass);
                });
            }

            internal void Dispose()
            {
                foreach (RTHandle target in history)
                    target?.Release();
            }
        }
    }
}
