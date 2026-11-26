using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaEffects
    {
        internal enum Cue
        {
            StepA, StepB, StepC, Jump, Land, Roll, Lock, DoorOpen, DoorShut, Mount, Brake,
            Equip, Reload, PanelOpen, PanelClose, Loot, Sale, Purchase, Alert, Cuffs, Reward, CarEngine, BikeEngine
        }

        internal struct Footfalls
        {
            private float distance;
            private int variant;

            internal void Update(CostaEffects effects, float meters, bool grounded, bool running, bool crouched,
                AudioSource spatial = null)
            {
                if (!grounded || meters > 1.5f)
                {
                    distance = 0;
                    return;
                }
                if (meters <= 0.001f)
                {
                    distance = Mathf.Min(distance, 0.28f);
                    return;
                }

                float stride = crouched ? 0.80f : running ? 1.92f : 1.04f;
                distance += meters;
                if (distance < stride)
                    return;

                distance %= stride;
                effects.Play((Cue)variant, crouched ? 0.19f : running ? 0.56f : 0.38f, spatial);
                variant = (variant + 1) % 3;
            }
        }

        private const int Rate = 22050;
        private readonly AudioClip[] bank = new AudioClip[(int)Cue.BikeEngine + 1];
        private readonly AudioSource feedback;
        private readonly AudioSource engine;
        private float nextBrake;

        internal CostaEffects(Transform parent)
        {
            feedback = NewSource("Gameplay cues", parent);
            engine = NewSource("Local vehicle engine", parent);
            engine.loop = true;
            engine.volume = 0;
        }

        internal void Play(Cue cue, float volume = 0.45f, AudioSource spatial = null)
        {
            AudioSource source = spatial != null ? spatial : feedback;
            if (source.isActiveAndEnabled && volume > 0)
                source.PlayOneShot(Get(cue), volume);
        }

        internal void Drive(CostaVehicle vehicle, float throttle)
        {
            float speed = vehicle != null ? Mathf.Clamp01(vehicle.Speed / vehicle.TopSpeed) : 0;
            float load = Mathf.Clamp01(Mathf.Abs(throttle));
            if (vehicle != null)
            {
                AudioClip clip = Get(vehicle.IsBike ? Cue.BikeEngine : Cue.CarEngine);
                if (engine.clip != clip)
                    engine.clip = clip;
                if (!engine.isPlaying)
                    engine.Play();
            }

            float blend = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            float volume = vehicle == null ? 0 : 0.06f + speed * 0.13f + load * 0.08f;
            engine.volume = Mathf.Lerp(engine.volume, volume, blend);
            engine.pitch = Mathf.Lerp(engine.pitch, 0.75f + speed * 0.85f + load * 0.19f, blend);
            if (vehicle == null && engine.volume < 0.003f && engine.isPlaying)
                engine.Stop();
        }

        internal void Brake(float previousSpeed, float speed)
        {
            float now = Time.unscaledTime;
            if (previousSpeed < 4f || previousSpeed - speed < 1.8f || now < nextBrake)
                return;
            Play(Cue.Brake, Mathf.Lerp(0.22f, 0.57f, Mathf.Clamp01(previousSpeed / 19f)));
            nextBrake = now + 0.33f;
        }

        internal void Release()
        {
            engine.Stop();
            feedback.Stop();
            foreach (AudioClip clip in bank)
                Object.Destroy(clip);
            Object.Destroy(engine.gameObject);
            Object.Destroy(feedback.gameObject);
        }

        private static AudioSource NewSource(string label, Transform parent)
        {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(parent, false);
            source.playOnAwake = false;
            source.priority = 96;
            return source;
        }

        private AudioClip Get(Cue cue) => bank[(int)cue] ??= Create(cue);

        private static AudioClip Create(Cue cue)
        {
            float length = cue switch
            {
                Cue.StepA or Cue.StepB or Cue.StepC => 0.15f,
                Cue.Jump or Cue.Mount or Cue.Equip or Cue.PanelOpen or Cue.PanelClose => 0.19f,
                Cue.Lock or Cue.DoorShut or Cue.Cuffs => 0.24f,
                Cue.DoorOpen or Cue.Land or Cue.Roll or Cue.Reload => 0.34f,
                Cue.Brake or Cue.Loot or Cue.Sale or Cue.Purchase => 0.40f,
                Cue.Alert or Cue.Reward => 0.56f,
                _ => 1f
            };
            var samples = new float[Mathf.RoundToInt(Rate * length)];
            var random = new System.Random(570 + (int)cue);
            float low = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / Rate;
                float noise = (float)(random.NextDouble() * 2 - 1);
                low += (noise - low) * 0.12f;
                float high = noise - low;
                if (cue is Cue.CarEngine or Cue.BikeEngine)
                {
                    // Integer fundamentals and harmonics finish at the same phase as they start.
                    float frequency = cue == Cue.BikeEngine ? 46 : 32;
                    samples[i] = 0.27f * Wave(time, frequency) + 0.14f * Wave(time, frequency * 2)
                        + 0.07f * Wave(time, frequency * 3) * (0.69f + 0.31f * Wave(time, 8))
                        + 0.04f * Wave(time, frequency * 5);
                    continue;
                }

                float sample = cue switch
                {
                    Cue.StepA or Cue.StepB or Cue.StepC => (0.56f * low + 0.32f * high
                        + Wave(time, 68 + 8 * (int)cue) * 0.27f) * Fade(time, 35)
                        + 0.24f * high * Fade(time - 0.048f, 63),
                    Cue.Jump => (0.43f * low + 0.34f * high) * Fade(time, 21),
                    Cue.Land => (0.65f * low + 0.32f * Wave(time, 54)) * Fade(time, 17)
                        + 0.30f * high * Fade(time - 0.041f, 32),
                    Cue.Roll => (0.40f * low + 0.21f * high)
                        * (0.65f * Fade(time, 16) + Fade(time - 0.11f, 13)),
                    Cue.Lock => Metal(time, high, 720) + 0.7f * Metal(time - 0.085f, high, 1020),
                    Cue.DoorOpen => (0.28f * Wave(time, 148 - time * 126) + 0.33f * low) * Fade(time, 12)
                        + 0.65f * Metal(time, high, 530),
                    Cue.DoorShut => (0.50f * low + 0.42f * Wave(time, 73)) * Fade(time, 23)
                        + 0.7f * Metal(time - 0.020f, high, 610),
                    Cue.Mount or Cue.Equip => Metal(time, high, 840) * 0.62f + 0.25f * low * Fade(time, 24),
                    Cue.Reload => 0.65f * Metal(time, high, 870) + 0.65f * Metal(time - 0.13f, high, 1140),
                    Cue.Brake => (0.32f * high + 0.13f * Wave(time, 630 - time * 270)) * Fade(time, 10),
                    Cue.PanelOpen => Tone(time, 480) + Tone(time - 0.032f, 680) * 0.68f,
                    Cue.PanelClose => Tone(time, 620) + Tone(time - 0.032f, 420) * 0.65f,
                    Cue.Loot => Tone(time, 720) + Tone(time - 0.08f, 980),
                    Cue.Sale => Tone(time, 1020) + Tone(time - 0.075f, 1350) + 0.53f * Tone(time - 0.15f, 1480),
                    Cue.Purchase => Tone(time, 660) + Tone(time - 0.072f, 910),
                    Cue.Alert => Tone(time, 246) + Tone(time - 0.09f, 320) + Tone(time - 0.22f, 272),
                    Cue.Cuffs => Metal(time, high, 1130) + 0.6f * Metal(time - 0.09f, high, 880),
                    _ => Tone(time, 660) + Tone(time - 0.09f, 825) + Tone(time - 0.19f, 940)
                };
                samples[i] = Mathf.Clamp(sample, -1f, 1f) * Mathf.Clamp01((length - time) * 38f);
            }

            AudioClip clip = AudioClip.Create("Costa " + cue, samples.Length, 1, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float Wave(float time, float frequency) => Mathf.Sin(2f * Mathf.PI * time * frequency);
        private static float Fade(float time, float speed) =>
            time < 0 ? 0 : Mathf.Clamp01(time * 375f) * Mathf.Exp(-speed * time);
        private static float Metal(float time, float noise, float frequency) =>
            (0.37f * noise + 0.38f * Wave(time, frequency) + 0.16f * Wave(time, frequency * 1.63f)) * Fade(time, 39);
        private static float Tone(float time, float frequency) =>
            (0.27f * Wave(time, frequency) + 0.10f * Wave(time, frequency * 2)) * Fade(time, 17);
    }
}
