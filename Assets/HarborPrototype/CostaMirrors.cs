using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CostaLivre
{
    internal sealed class CostaMirrors
    {
        private sealed class Surface
        {
            internal Renderer Renderer;
            internal CostaVehicle Vehicle;
            internal int Slot = -1;
            internal float Updated = -100;
        }

        private sealed class Slot
        {
            internal RenderTexture Texture;
            internal Surface Surface;
            internal float Time;
        }

        private static readonly int ReflectionTexture = Shader.PropertyToID("_ReflectionTexture");
        private static readonly int ReflectionViewProjection = Shader.PropertyToID("_ReflectionViewProjection");
        private static readonly int ReflectionAmount = Shader.PropertyToID("_ReflectionAmount");
        private static readonly ProfilerMarker CaptureMarker = new("Costa.Mirror.Capture");
        private readonly List<Surface> surfaces = new();
        private readonly MaterialPropertyBlock properties = new();
        private readonly Plane[] frustum = new Plane[6];
        private readonly Slot[] slots = { new(), new(), new() };
        private readonly Camera viewer;
        private readonly Material glass;
        private Camera reflected;
        private readonly UniversalRenderPipeline.SingleCameraRequest request = new();
        private float nextCapture;
        private int resolution;
        internal int Captures { get; private set; }

        internal CostaMirrors(Camera viewer)
        {
            this.viewer = viewer;
            Shader shader = Resources.Load<Shader>("Harbor/PlanarMirror");
            if (shader != null)
                glass = new Material(shader) { name = "Costa Planar Mirror" };
        }

        internal void Install(Transform parent, Vector3 position, Vector3 normal, Vector2 dimensions, CostaVehicle vehicle = null)
        {
            if (glass == null)
                return;
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(panel.GetComponent<Collider>());
            panel.name = "Planar mirror glass";
            panel.transform.SetParent(parent, false);
            panel.transform.localPosition = position;
            panel.transform.localRotation = Quaternion.LookRotation(-normal);
            panel.transform.localScale = new Vector3(dimensions.x, dimensions.y, 1);
            Renderer renderer = panel.GetComponent<Renderer>();
            renderer.sharedMaterial = glass;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            surfaces.Add(new Surface { Renderer = renderer, Vehicle = vehicle });
        }

        internal void Tick(int preset, int quality, bool active, CostaVehicle car)
        {
            int requested = MirrorResolution(preset, quality);
            if (requested != resolution)
            {
                foreach (Slot slot in slots)
                    Clear(slot, true);
                resolution = requested;
            }

            float now = Time.unscaledTime;
            if (!active || resolution == 0 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                foreach (Slot slot in slots)
                    if (slot.Surface != null)
                        Clear(slot, false);
                return;
            }

            foreach (Slot slot in slots)
                if (slot.Surface != null &&
                    (slot.Surface.Renderer == null || !slot.Surface.Renderer.gameObject.activeInHierarchy ||
                        slot.Surface.Vehicle != null && slot.Surface.Vehicle != car || now - slot.Time > .46f))
                    Clear(slot, false);
            if (surfaces.Count == 0 || now < nextCapture)
                return;

            GeometryUtility.CalculateFrustumPlanes(viewer, frustum);
            Vector3 cameraPosition = viewer.transform.position;
            Surface nearest = null;
            float best = float.PositiveInfinity;
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                Surface surface = surfaces[i];
                Renderer renderer = surface.Renderer;
                if (renderer == null)
                {
                    surfaces.RemoveAt(i);
                    continue;
                }
                if (!renderer.gameObject.activeInHierarchy || surface.Vehicle != null && surface.Vehicle != car)
                    continue;

                Vector3 delta = cameraPosition - renderer.transform.position;
                float dist = delta.sqrMagnitude;
                float age = Mathf.Clamp(now - surface.Updated, .01f, 3);
                float priority = dist / age;
                if (dist > 10 * 10 || priority >= best || Vector3.Dot(delta, -renderer.transform.forward) < .06f ||
                    !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds))
                    continue;
                if (Physics.Linecast(cameraPosition, renderer.transform.position, out RaycastHit hit,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                    (surface.Vehicle == null || !surface.Vehicle.OwnsCollider(hit.collider)))
                    continue;
                nearest = surface;
                best = priority;
            }
            if (nearest == null)
                return;

            nextCapture = now + (preset >= 3 ? .085f : preset == 2 ? .14f : .20f);
            int index = nearest.Slot;
            if (index < 0)
            {
                int limit = preset >= 2 ? slots.Length : 1;
                index = 0;
                for (int i = 1; i < limit; i++)
                    if (slots[i].Surface == null || slots[i].Time < slots[index].Time)
                        index = i;
                Clear(slots[index], false);
            }
            Capture(nearest, index, now);
        }

        internal static int MirrorResolution(int preset, int quality) => quality switch
        {
            1 => 0,
            2 => 128,
            3 => 256,
            4 => 512,
            _ => preset switch { >= 3 => 512, 2 => 256, 1 => 128, _ => 0 }
        };

        private void Capture(Surface surface, int index, float now)
        {
            using var profile = CaptureMarker.Auto();
            if (reflected == null)
            {
                reflected = new GameObject("Planar reflection camera").AddComponent<Camera>();
                reflected.transform.SetParent(viewer.transform, false);
                reflected.enabled = false;
                var settings = reflected.GetUniversalAdditionalCameraData();
                settings.renderPostProcessing = false;
                settings.renderShadows = false;
                settings.requiresDepthOption = CameraOverrideOption.Off;
                settings.requiresColorOption = CameraOverrideOption.Off;
                settings.antialiasing = AntialiasingMode.None;
            }

            Slot slot = slots[index];
            int height = Mathf.Max(64, Mathf.RoundToInt(resolution / Mathf.Clamp(viewer.aspect, 1, 2.8f)));
            if (slot.Texture != null && slot.Texture.height != height)
                Clear(slot, true);
            if (slot.Texture == null)
            {
                slot.Texture = new RenderTexture(resolution, height, 16, RenderTextureFormat.ARGB32)
                    { name = "Costa mirror capture", useMipMap = false, wrapMode = TextureWrapMode.Clamp };
                slot.Texture.Create();
            }
            Transform panel = surface.Renderer.transform;
            Vector3 normal = -panel.forward;
            Vector3 position = panel.position;
            Matrix4x4 reflection = ReflectionMatrix(position, normal);
            Matrix4x4 view = viewer.worldToCameraMatrix * reflection;
            reflected.transform.SetPositionAndRotation(reflection.MultiplyPoint(viewer.transform.position),
                Quaternion.LookRotation(reflection.MultiplyVector(viewer.transform.forward),
                    reflection.MultiplyVector(viewer.transform.up)));
            reflected.worldToCameraMatrix = view;
            reflected.fieldOfView = viewer.fieldOfView;
            reflected.aspect = viewer.aspect;
            reflected.nearClipPlane = viewer.nearClipPlane;
            reflected.farClipPlane = surface.Vehicle == null ? 28 : 42;
            reflected.cullingMask = viewer.cullingMask;
            reflected.clearFlags = viewer.clearFlags;
            reflected.backgroundColor = viewer.backgroundColor;
            reflected.allowHDR = false;
            reflected.allowMSAA = false;
            reflected.useOcclusionCulling = false;
            reflected.ResetProjectionMatrix();
            Vector3 clipPoint = view.MultiplyPoint(position + normal * .018f);
            Vector3 clipNormal = view.MultiplyVector(normal).normalized;
            reflected.projectionMatrix = reflected.CalculateObliqueMatrix(
                new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal)));
            request.destination = slot.Texture;
            if (!RenderPipeline.SupportsRenderRequest(reflected, request))
                return;

            bool previous = GL.invertCulling;
            try
            {
                for (int i = 0; i < surfaces.Count; i++)
                    if (surfaces[i].Renderer != null)
                        surfaces[i].Renderer.forceRenderingOff = true;
                GL.invertCulling = !previous;
                RenderPipeline.SubmitRenderRequest(reflected, request);
            }
            finally
            {
                GL.invertCulling = previous;
                for (int i = 0; i < surfaces.Count; i++)
                    if (surfaces[i].Renderer != null)
                        surfaces[i].Renderer.forceRenderingOff = false;
            }

            surface.Slot = index;
            surface.Updated = slot.Time = now;
            slot.Surface = surface;
            properties.Clear();
            properties.SetTexture(ReflectionTexture, slot.Texture);
            properties.SetMatrix(ReflectionViewProjection, GL.GetGPUProjectionMatrix(reflected.projectionMatrix, true) * view);
            properties.SetFloat(ReflectionAmount, 1);
            surface.Renderer.SetPropertyBlock(properties);
            Captures++;
        }

        internal static Matrix4x4 ReflectionMatrix(Vector3 point, Vector3 normal)
        {
            Matrix4x4 matrix = Matrix4x4.identity;
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                    matrix[row, col] -= 2 * normal[row] * normal[col];
                matrix[row, 3] = 2 * normal[row] * Vector3.Dot(normal, point);
            }
            return matrix;
        }

        private static void Clear(Slot slot, bool destroy)
        {
            if (slot.Surface != null)
            {
                slot.Surface.Slot = -1;
                if (slot.Surface.Renderer != null)
                    slot.Surface.Renderer.SetPropertyBlock(null);
                slot.Surface = null;
            }
            if (destroy && slot.Texture != null)
            {
                slot.Texture.Release();
                Object.Destroy(slot.Texture);
                slot.Texture = null;
            }
        }

        internal void Release()
        {
            foreach (Slot slot in slots)
                Clear(slot, true);
            Object.Destroy(glass);
            if (reflected != null)
                Object.Destroy(reflected.gameObject);
            surfaces.Clear();
        }
    }
}
