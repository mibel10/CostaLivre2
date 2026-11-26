using System.Collections.Generic;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class HarborStreetLights
    {
        private const int MaxLights = 10;
        private const float ScanRangeSquared = 66f * 66f;
        private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Warm = new(1f, 0.68f, 0.34f);

        private readonly Transform root;
        private readonly List<Vector3> lamps = new();
        private readonly Light[] lights = new Light[MaxLights];
        private readonly Vector3[] nearest = new Vector3[MaxLights];
        private readonly float[] distances = new float[MaxLights];
        private int activeLights;
        private int previousBudget;
        private double nextScan;
        private bool lit;

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

        internal void Update(bool night, Vector3 view, int budget)
        {
            if (lit != night)
            {
                lit = night;
                Diffuser.SetColor(Emission, night ? Warm * 2.65f : Color.black);
                nextScan = 0;
            }

            if (!night || budget <= 0)
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
                if (!light.enabled)
                    light.enabled = true;
            }
            TurnOffFrom(count);
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
            light.intensity = 8.5f;
            light.range = 13f;
            light.spotAngle = 110f;
            light.innerSpotAngle = 60f;
            light.shadows = LightShadows.None;
            return light;
        }

        private void TurnOffFrom(int count)
        {
            for (int i = count; i < activeLights; i++)
                lights[i].enabled = false;
            activeLights = count;
        }

        internal void Release()
        {
            foreach (Light light in lights)
                if (light != null)
                    Object.Destroy(light.gameObject);
            Object.Destroy(Diffuser);
        }
    }
}
