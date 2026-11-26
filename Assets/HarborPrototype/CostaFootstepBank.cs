using UnityEngine;
using Floor = CostaLivre.CostaAcousticScene.Floor;

namespace CostaLivre
{
    internal sealed class CostaFootstepBank
    {
        private const int Rate = 22050;
        private const int Variants = 4;
        internal const int Floors = (int)Floor.Carpet + 1;

        private readonly AudioClip[,] clips = new AudioClip[Floors, Variants];
        private readonly System.Random choice = new();

        internal AudioClip Pick(Floor surface, ref int previous)
        {
            int index = choice.Next(Variants - 1);
            if (index >= previous)
                index++;
            previous = index;
            return clips[(int)surface, index] ??= Build(surface, index);
        }

        internal void Warm(int surface)
        {
            for (int variant = 0; variant < Variants; variant++)
                clips[surface, variant] ??= Build((Floor)surface, variant);
        }

        private static AudioClip Build(Floor surface, int variant)
        {
            var random = new System.Random(971 + (int)surface * 41 + variant * 17);
            float weight = .86f + .075f * variant;
            float toeDelay = .045f + .009f * variant;
            float length = surface == Floor.Metal ? .33f : .24f;
            var samples = new float[Mathf.RoundToInt(length * Rate)];
            float bass = 0, texture = 0, grit = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(random.NextDouble() * 2 - 1);
                bass += (noise - bass) * .039f;
                texture += (noise - texture) * .19f;
                float edge = noise - texture;
                float grain = texture - bass;
                if (i % (7 + 2 * variant) == 0)
                    grit = (float)(random.NextDouble() * 2 - 1);
                float heel = Envelope(t, 37);
                float toe = Envelope(t - toeDelay, 49);
                float thump = (Bass(t, (70 + 4 * variant) * weight) * .56f + bass * 1.45f) * heel;
                float sample = surface switch
                {
                    Floor.Asphalt => thump * .65f + (.51f * edge + .23f * grit + .27f * grain) * (heel * .73f + toe * .74f),
                    Floor.Earth => thump * .47f + (.38f * bass + .47f * grain + .21f * grit) * (heel + toe * .85f)
                        + grit * edge * .17f * Envelope(t - toeDelay * .52f, 24),
                    Floor.Wood => thump * .65f + Resonance(t, 181 * weight, 25) * .27f
                        + Resonance(t, 315 * weight, 34) * .15f + (.32f * grain + .17f * edge) * toe,
                    Floor.Metal => thump * .43f + Resonance(t, 487 * weight, 17) * .23f
                        + Resonance(t, 782 * weight, 23) * .16f + Resonance(t - toeDelay, 1061 * weight, 29) * .12f
                        + (.22f * edge + .14f * grit) * (heel + toe * .51f),
                    Floor.Carpet => thump * .40f + (.71f * bass + .20f * grain) * (heel * .55f + toe * .68f),
                    _ => thump * .66f + (.35f * edge + .31f * grain) * (heel + toe * .58f)
                        + Resonance(t, 421 * weight, 59) * .14f
                };
                samples[i] = Mathf.Clamp(sample * weight, -.95f, .95f) * Mathf.Clamp01((length - t) * 56);
            }
            AudioClip clip = AudioClip.Create($"{surface} step {variant + 1}", samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Envelope(float t, float decay) =>
            t < 0 ? 0 : Mathf.Clamp01(t * 330f) * Mathf.Exp(-t * decay);

        private static float Bass(float t, float hz) => Mathf.Sin(2 * Mathf.PI * hz * t);
        private static float Resonance(float t, float hz, float decay) => Bass(t, hz) * Envelope(t, decay);

        internal void Release()
        {
            foreach (AudioClip clip in clips)
                Object.Destroy(clip);
        }
    }
}
