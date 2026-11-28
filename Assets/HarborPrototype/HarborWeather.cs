using UnityEngine;
using UnityEngine.Rendering;

namespace CostaLivre
{
    internal sealed class HarborWeather
    {
        internal readonly struct Conditions
        {
            internal readonly float Rain, Mist, Clouds, Wet, Flash;

            internal Conditions(float rain, float mist, float clouds, float wet, float flash)
            {
                Rain = rain;
                Mist = mist;
                Clouds = clouds;
                Wet = wet;
                Flash = flash;
            }

            // Use the same continuous clock as the sun on the host and every guest.
            internal static Conditions At(float hour)
            {
                float mist = Window(hour, 2f, 5f, 6.2f, 8f);
                float clouds = Window(hour, 11f, 14f, 17.6f, 21f);
                float rain = Window(hour, 13f, 15.2f, 17.2f, 19.5f);
                float wet = Window(hour, 13.4f, 16f, 19.5f, 23.9f);
                float flash = Mathf.Max(Pulse(hour, 15.42f), Mathf.Max(Pulse(hour, 16.34f), Pulse(hour, 17.06f)));
                return new Conditions(rain, mist, clouds, wet, flash * rain);
            }

            private static float Window(float hour, float start, float peak, float hold, float end) =>
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, peak, hour)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hold, end, hour)));

            private static float Pulse(float hour, float start)
            {
                float elapsed = hour - start;
                return elapsed < 0 || elapsed > 0.035f ? 0 : Mathf.Exp(-elapsed * 195f);
            }
        }

        private readonly Transform root;
        private ParticleSystem rain;
        private Material rainMaterial;
        private ReflectionProbe[] probes;
        private ReflectionProbe capturing;
        private ReflectionProbe lastCaptured;
        private float nextProbeSearch;
        private int captureId;
        private float nextCapture;
        private float lastWet = -1;
        private int previousBudget = -1;
        private int rainPreset = -1;
        private int rainLevel = -1;
        private Vector3 priorCenter;
        private float rainRange;

        internal HarborWeather(Transform root) => this.root = root;

        internal void Apply(float? hour, Conditions weather, Camera camera, int preset, HarborBlockout art)
        {
            float wet = Mathf.Round(weather.Wet * 32) / 32;
            if (wet != lastWet)
            {
                lastWet = wet;
                art.SetSurfaceWetness(wet);
            }
            if (hour.HasValue && weather.Rain > 0.015f && rain == null)
                CreateRain();
            if (rain != null)
            {
                int level = hour.HasValue && weather.Rain > .015f ? Mathf.Max(1, Mathf.RoundToInt(weather.Rain * 48)) : 0;
                var emission = rain.emission;
                if (rainPreset != preset)
                {
                    rainPreset = preset;
                    rainLevel = -1;
                    var collision = rain.collision;
                    collision.enabled = preset >= 3;
                    collision.quality = ParticleSystemCollisionQuality.Low;
                }
                if (rainLevel != level)
                {
                    rainLevel = level;
                    emission.rateOverTime = (preset >= 3 ? 520 : preset == 2 ? 300 : 160) * level / 48f;
                    emission.enabled = level > 0;
                }
                if (level > 0)
                {
                    Vector3 center = camera.transform.position;
                    if ((center - priorCenter).sqrMagnitude > rainRange * rainRange)
                        rain.Clear();
                    priorCenter = center;
                    rain.transform.position = center + Vector3.up * 12;
                    if (!rain.isPlaying)
                        rain.Play();
                }
                else if (rain.isEmitting || !hour.HasValue && rain.isPlaying)
                    rain.Stop(true, hour.HasValue ? ParticleSystemStopBehavior.StopEmitting
                        : ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            UpdateReflections(hour.HasValue && preset >= 4, camera.transform.position, preset);
        }

        private void CreateRain()
        {
            Shader shader = Resources.Load<Shader>("Harbor/CoastalRain");
            if (shader == null)
                return;

            GameObject emitter = new GameObject("World rain");
            emitter.SetActive(false);
            emitter.transform.SetParent(root, false);
            rain = emitter.AddComponent<ParticleSystem>();
            var main = rain.main;
            main.playOnAwake = false;
            main.useUnscaledTime = true;
            main.maxParticles = 640;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.026f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.62f, 0.92f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = rain.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            rainRange = 27;
            shape.scale = new Vector3(rainRange, 1, rainRange);
            var velocity = rain.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(2.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(-20f);
            velocity.z = new ParticleSystem.MinMaxCurve(0.8f);
            var collision = rain.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.enableDynamicColliders = false;
            collision.lifetimeLoss = 1;
            collision.maxCollisionShapes = 16;

            var renderer = emitter.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.8f;
            renderer.velocityScale = 0.016f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            rainMaterial = new Material(shader);
            renderer.sharedMaterial = rainMaterial;
            emitter.SetActive(true);
        }

        private void UpdateReflections(bool active, Vector3 view, int preset)
        {
            int budget = active ? preset : 0;
            if (previousBudget != budget)
            {
                previousBudget = budget;
                if (active && probes == null)
                    CreateProbes();
                if (probes != null)
                    foreach (ReflectionProbe probe in probes)
                    {
                        probe.enabled = active;
                        if (active)
                            probe.resolution = preset >= 4 ? 128 : 64;
                    }
                capturing = null;
                lastCaptured = null;
                nextCapture = 0;
                nextProbeSearch = 0;
            }
            float now = Time.unscaledTime;
            if (!active || now < nextProbeSearch)
                return;
            nextProbeSearch = now + 0.6f;
            if (capturing != null && captureId >= 0 && !capturing.IsFinishedRendering(captureId))
                return;

            ReflectionProbe nearest = null;
            float distance = 110 * 110;
            foreach (ReflectionProbe probe in probes)
            {
                float squared = (probe.transform.position - view).sqrMagnitude;
                if (squared < distance)
                {
                    nearest = probe;
                    distance = squared;
                }
            }
            if (nearest == null || (nearest == lastCaptured && now < nextCapture))
                return;

            capturing = nearest;
            lastCaptured = nearest;
            captureId = capturing.RenderProbe();
            nextCapture = now + (preset >= 4 ? 10 : 16);
        }

        private void CreateProbes()
        {
            Vector3[] centers =
            {
                new(0, 0, 2), new(-60, 0, 37), new(-10, 0, 42), new(-108, 0, 88),
                new(-50, 0, 97), new(40, 0, 107), new(0, 0, 265), new(0, 0, 495), new(0, 0, 721)
            };
            probes = new ReflectionProbe[centers.Length];
            for (int i = 0; i < probes.Length; i++)
            {
                GameObject anchor = new GameObject("Street reflection capture");
                anchor.transform.SetParent(root, false);
                anchor.transform.localPosition = centers[i] + Vector3.up * 2.4f;
                var probe = anchor.AddComponent<ReflectionProbe>();
                probe.mode = ReflectionProbeMode.Realtime;
                probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
                probe.size = new Vector3(107, 37, 107);
                probe.boxProjection = true;
                probe.blendDistance = 12;
                probe.hdr = true;
                probe.nearClipPlane = 0.2f;
                probe.farClipPlane = 85;
                probe.shadowDistance = 23;
                probes[i] = probe;
            }
        }

        internal void Release()
        {
            if (rain != null)
                Object.Destroy(rain.gameObject);
            Object.Destroy(rainMaterial);
            if (probes != null)
                foreach (ReflectionProbe probe in probes)
                    Object.Destroy(probe.gameObject);
        }
    }
}
