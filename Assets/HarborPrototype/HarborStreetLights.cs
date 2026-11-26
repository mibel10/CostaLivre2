using System.Collections.Generic;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class HarborStreetLights
    {
        private const int MaxLights = 10;
        private const int ScatteringLights = 2;
        private const float Range = 13f;
        private const float Intensity = 8.5f;
        private const float OuterCone = 110;
        private const float InnerCone = 60;
        private static readonly int FogLightCount = Shader.PropertyToID("_CostaFogLightCount");
        private static readonly int FogLights = Shader.PropertyToID("_CostaFogLights");
        private static readonly int FogLightColor = Shader.PropertyToID("_CostaFogLightColor");
        private const float ScanRangeSquared = 66f * 66f;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Warm = new(1f, 0.68f, 0.34f);

        private readonly Transform root;
        private readonly List<Vector3> lamps = new();
        private readonly Light[] lights = new Light[MaxLights];
        private readonly Vector3[] nearest = new Vector3[MaxLights];
        private readonly float[] distances = new float[MaxLights];
        private readonly Vector4[] fogLights = new Vector4[ScatteringLights];
        private int activeLights;
        private int previousBudget;
        private double nextScan;
        private int brightness = -1;

        internal Material Diffuser { get; }

        internal HarborStreetLights(Transform root, Shader shader)
        {
            this.root = root;
            Material authored = Resources.Load<Material>("Harbor/StreetLamp");
            Diffuser = authored != null ? new Material(authored) : new Material(shader);
            Diffuser.name = "Street lamp diffuser";
            Diffuser.color = new Color(0.50f, 0.49f, 0.43f);
            Diffuser.EnableKeyword("_EMISSION");
            Diffuser.SetColor(Emission, Color.black);
        }

        internal void Add(Vector3 position) => lamps.Add(position);

        internal void Update(float night, Vector3 view, int budget)
        {
            int level = Mathf.RoundToInt(night * 32);
            if (brightness != level)
            {
                brightness = level;
                Diffuser.SetColor(Emission, Warm * (2.65f * level / 32f));
                for (int i = 0; i < activeLights; i++)
                    lights[i].intensity = Intensity * level / 32f;
                nextScan = 0;
            }

            if (level == 0 || budget <= 0)
            {
                TurnOffFrom(0);
                previousBudget = 0;
                return;
            }

            budget = Mathf.Min(budget, MaxLights);
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < nextScan && budget == previousBudget)
                return;
            previousBudget = budget;
            nextScan = now + 0.23;
            int count = SelectNearest(view, budget);
            for (int i = 0; i < count; i++)
            {
                Light light = lights[i] ??= CreateLight();
                light.transform.position = nearest[i];
                light.intensity = Intensity * level / 32f;
                if (!light.enabled)
                    light.enabled = true;
            }
            TurnOffFrom(count);
            int fogCount = Mathf.Min(count, ScatteringLights);
            for (int i = 0; i < fogCount; i++)
                fogLights[i] = new Vector4(nearest[i].x, nearest[i].y, nearest[i].z, Range);
            if (fogCount > 0)
            {
                Color color = Warm.linear * (Intensity * level / 32f);
                Shader.SetGlobalVector(FogLightColor, new Vector4(color.r, color.g, color.b,
                    Mathf.Cos(OuterCone * Mathf.Deg2Rad * 0.5f)));
                Shader.SetGlobalVectorArray(FogLights, fogLights);
            }
            Shader.SetGlobalFloat(FogLightCount, fogCount);
        }

        private int SelectNearest(Vector3 view, int budget)
        {
            int count = 0;
            foreach (Vector3 position in lamps)
            {
                float distance = (position - view).sqrMagnitude;
                if (distance > ScanRangeSquared || (count == budget && distance >= distances[count - 1]))
                    continue;

                int insert = Mathf.Min(count, budget - 1);
                while (insert > 0 && distance < distances[insert - 1])
                {
                    nearest[insert] = nearest[insert - 1];
                    distances[insert] = distances[insert - 1];
                    insert--;
                }
                nearest[insert] = position;
                distances[insert] = distance;
                count = Mathf.Min(count + 1, budget);
            }
            return count;
        }

        private Light CreateLight()
        {
            GameObject obj = new("Street lamp light");
            obj.transform.SetParent(root, false);
            obj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light light = obj.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = Warm;
            light.intensity = Intensity;
            light.range = Range;
            light.spotAngle = OuterCone;
            light.innerSpotAngle = InnerCone;
            light.shadows = LightShadows.None;
            return light;
        }

        private void TurnOffFrom(int count)
        {
            for (int i = count; i < activeLights; i++)
                lights[i].enabled = false;
            if (count == 0 && activeLights != 0)
                Shader.SetGlobalFloat(FogLightCount, 0);
            activeLights = count;
        }

        internal void Release()
        {
            TurnOffFrom(0);
            foreach (Light light in lights)
                if (light != null)
                    Object.Destroy(light.gameObject);
            Object.Destroy(Diffuser);
        }
    }
}
