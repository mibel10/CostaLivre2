using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaEffects
    {
        internal enum Cue
        {
            Jump = 3, Land, Roll, Lock, DoorOpen, DoorShut, Mount, Brake,
            Equip, PanelOpen, PanelClose, Loot, Sale, Purchase, Alert, Cuffs, Reward, CarEngine, BikeEngine,
            Concrete, MetalHit, Wood, Glass, Earth, Blast, Incoming, Hurt, ReloadOut, ReloadIn, ReloadDone, LiftChime
        }

        internal struct Footfalls
        {
            private float distance;
            private int variant;
            private bool left;

            internal void Update(CostaEffects effects, Transform character, float meters, bool grounded, bool running, bool crouched)
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
                left = !left;
                Vector3 foot = character.position + character.right * (left ? -.13f : .13f);
                effects.Step(foot, crouched ? .24f : running ? .54f : .39f, ref variant);
            }
        }

        private const int Rate = 22050;
        private readonly AudioClip[] bank = new AudioClip[(int)Cue.LiftChime + 1];
        private readonly AudioSource feedback;
        private readonly CostaSpatialAudio spatialAudio;
        private readonly CostaAcousticScene acousticScene;
        private readonly CostaWorldDamage surfaces;
        private readonly CostaFootstepBank footsteps = new();
        private readonly CostaSpatialAudio.Voice engine;
        private AudioClip engineClip;
        private float engineGain;
        private float enginePitch = .75f;
        private float nextBrake;
        private readonly CostaImpactParticles particles;
        private float nextPressure;
        private float pressureAt = -10;
        private float hurtAt = -10;
        private float strength;
        private Vector3 sourcePoint;
        private Texture2D edgeFade;

        internal CostaEffects(Transform parent, Transform listener, HarborBlockout art)
        {
            feedback = NewSource("Gameplay cues", parent);
            feedback.reverbZoneMix = 0;
            acousticScene = art.Acoustics;
            surfaces = art.Damage;
            spatialAudio = new CostaSpatialAudio(parent, listener, acousticScene, surfaces);
            engine = spatialAudio.LoopVoice("Local vehicle engine");
            particles = new CostaImpactParticles(parent);
        }

        internal CostaSpatialAudio WorldAudio => spatialAudio;

        internal void UpdateWorldAudio(bool inWorld) => spatialAudio.Update(inWorld);

        internal void WarmSteps(int surface) => footsteps.Warm(surface);

        private void Step(Vector3 foot, float volume, ref int variant)
        {
            const float far = 23f;
            if ((foot - spatialAudio.Listener).sqrMagnitude >= far * far)
                return;
            var floor = acousticScene.FloorAt(foot, surfaces);
            spatialAudio.Play(footsteps.Pick(floor, ref variant), foot + Vector3.up * .17f, volume, 3.3f, far, 117);
        }

        internal void Play(Cue cue, float volume = 0.45f)
        {
            if (volume > 0)
                feedback.PlayOneShot(Get(cue), volume);
        }

        internal void SurfaceHit(CostaWorldDamage.Surface surface, Vector3 point, Vector3 normal, bool broken, Vector3 listener)
        {
            if ((listener - point).sqrMagnitude > 56 * 56)
                return;
            particles.Burst(surface, point, normal, broken, Application.isMobilePlatform);
            PlayAt((Cue)((int)Cue.Concrete + (int)surface), point, broken ? .60f : .25f);
        }

        internal void Explosion(Vector3 point)
        {
            particles.Blast(point, Application.isMobilePlatform);
            PlayAt(Cue.Blast, point, .68f);
        }

        internal void PlayAt(Cue cue, Vector3 point, float volume)
        {
            float far = cue == Cue.Blast ? 85 : cue == Cue.Incoming ? 18 : 32;
            spatialAudio.Play(Get(cue), point, volume, cue == Cue.Blast ? 8 : 2, far, 100);
        }

        internal void PlayShot(AudioClip clip, Vector3 point, float volume, bool melee = false) =>
            spatialAudio.Play(clip, point, volume, melee ? 2 : 5.5f, melee ? 23 : 93, 68);

        internal void CombatFeedback(Vector3 from, int damage)
        {
            float now = Time.unscaledTime;
            if (damage > 0)
            {
                bool sound = now - hurtAt > .085f;
                strength = Mathf.Clamp01(Mathf.Max((now - hurtAt < .12f ? strength * .65f : 0),
                    .23f + damage / 92f));
                hurtAt = now;
                sourcePoint = from;
                if (sound)
                    Play(Cue.Hurt, Mathf.Lerp(.25f, .47f, strength));
            }
            else if (now >= nextPressure)
            {
                pressureAt = now;
                nextPressure = now + .46f;
                PlayAt(Cue.Incoming, from, .34f);
            }
        }

        internal void ResetCombat()
        {
            hurtAt = pressureAt = -10;
            nextPressure = 0;
        }

        internal void DrawCombat(Camera camera)
        {
            float damage = Mathf.Clamp01(1 - (Time.unscaledTime - hurtAt) / .86f) * strength;
            float pressure = Mathf.Clamp01(1 - (Time.unscaledTime - pressureAt) / .39f);
            if (damage <= 0 && pressure <= 0)
                return;

            float w = Screen.width, h = Screen.height;
            float edge = Mathf.Min(w, h) * .12f;
            Color previous = GUI.color;
            GUI.color = new Color(.54f + damage * .23f, .32f - damage * .13f, .22f,
                .26f * damage + .12f * pressure);
            edgeFade ??= CreateEdgeFade();
            GUI.DrawTexture(new Rect(0, 0, edge, h), edgeFade);
            GUI.DrawTextureWithTexCoords(new Rect(w - edge, 0, edge, h), edgeFade, new Rect(1, 0, -1, 1));
            if (damage > 0)
            {
                Vector3 relative = camera.transform.InverseTransformPoint(sourcePoint);
                float bearing = Mathf.Atan2(relative.x, relative.z) * Mathf.Rad2Deg;
                Vector2 center = new(w * .5f, h * .5f);
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(bearing, center);
                GUI.color = new Color(.94f, .42f, .29f, damage * .88f);
                float offset = Mathf.Min(w, h) * .15f;
                GUI.DrawTexture(new Rect(center.x - offset * .145f, center.y - offset, offset * .29f, edge * .045f),
                    Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
            GUI.color = previous;
        }

        private static Texture2D CreateEdgeFade()
        {
            var texture = new Texture2D(48, 1, TextureFormat.RGBA32, false)
                { name = "Combat edge fade", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[texture.width];
            for (int i = 0; i < pixels.Length; i++)
            {
                float alpha = 1 - Mathf.SmoothStep(0, 1, i / (float)(pixels.Length - 1));
                pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        internal void Drive(CostaVehicle vehicle, float throttle)
        {
            float speed = vehicle != null ? Mathf.Clamp01(vehicle.Speed / vehicle.TopSpeed) : 0;
            float load = Mathf.Clamp01(Mathf.Abs(throttle));
            float blend = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
            float volume = vehicle == null ? 0 : 0.07f + speed * 0.15f + load * 0.09f;
            engineGain = Mathf.Lerp(engineGain, volume, blend);
            enginePitch = Mathf.Lerp(enginePitch, 0.75f + speed * 0.85f + load * 0.19f, blend);
            if (vehicle != null)
            {
                AudioClip clip = Get(vehicle.IsBike ? Cue.BikeEngine : Cue.CarEngine);
                Vector3 motor = vehicle.Root.TransformPoint(new Vector3(0, .60f, vehicle.IsBike ? 0 : .72f));
                if (!engine.Playing || engineClip != clip)
                    engine.Play(clip, motor, engineGain, 4f, 32f, 84, enginePitch, true, spatialAudio);
                engineClip = clip;
                engine.Move(motor, engineGain, enginePitch);
            }
            else if (engine.Playing)
            {
                if (engineGain <= .003f)
                    engine.Stop();
                else
                    engine.Move(engine.Position, engineGain, enginePitch);
            }
            engine.Update(spatialAudio, blend, .12f);
        }

        internal void Brake(float previousSpeed, float speed)
        {
            float now = Time.unscaledTime;
            if (previousSpeed < 4f || previousSpeed - speed < 1.8f || now < nextBrake)
                return;
            Play(Cue.Brake, Mathf.Lerp(0.22f, 0.57f, Mathf.Clamp01(previousSpeed / 19f)));
            nextBrake = now + 0.33f;
        }

        internal void Skid(float slip, float speed)
        {
            if (slip < 5 || speed < 5.5f || Time.unscaledTime < nextBrake)
                return;
            Play(Cue.Brake, Mathf.Lerp(.12f, .36f, Mathf.InverseLerp(5, 13, slip)));
            nextBrake = Time.unscaledTime + .45f;
        }

        internal void Release()
        {
            particles.Release();
            Object.Destroy(edgeFade);
            spatialAudio.Release();
            footsteps.Release();
            feedback.Stop();
            foreach (AudioClip clip in bank)
                Object.Destroy(clip);
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
                Cue.Jump or Cue.Mount or Cue.Equip or Cue.PanelOpen or Cue.PanelClose => 0.19f,
                Cue.Lock or Cue.DoorShut or Cue.Cuffs => 0.24f,
                Cue.DoorOpen or Cue.Land or Cue.Roll => 0.34f,
                Cue.Brake or Cue.Loot or Cue.Sale or Cue.Purchase => 0.40f,
                Cue.Alert or Cue.Reward or Cue.LiftChime => 0.56f,
                Cue.Concrete or Cue.Wood or Cue.Earth or Cue.Hurt => 0.20f,
                Cue.MetalHit or Cue.Glass or Cue.Incoming => 0.30f,
                Cue.Blast => 0.70f,
                Cue.ReloadOut or Cue.ReloadIn or Cue.ReloadDone => .13f,
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
                    Cue.Concrete => (0.74f * low + .37f * high) * Fade(time, 43) + .22f * high * Fade(time - .027f, 56),
                    Cue.MetalHit => .55f * Metal(time, high, 1530) + .42f * Wave(time, 2316) * Fade(time, 19),
                    Cue.Wood => (.65f * low + .26f * Wave(time, 287)) * Fade(time, 41) + .22f * high * Fade(time - .014f, 66),
                    Cue.Glass => (.59f * high + .27f * Wave(time, 3297)) * Fade(time, 49)
                        + .33f * Metal(time - .024f, high, 2490) + .18f * Metal(time - .065f, high, 3954),
                    Cue.Earth => (.82f * low + .13f * high) * Fade(time, 31),
                    Cue.Hurt => (.59f * low + .32f * Wave(time, 74)) * Fade(time, 27),
                    Cue.Incoming => (.37f * high + .19f * Wave(time, 1650 - 2730 * time))
                        * Fade(time, 21) * Mathf.Clamp01(time * 53),
                    Cue.Blast => (.87f * low + .51f * Wave(time, 66 - 72 * time)) * Fade(time, 8)
                        + .35f * high * Fade(time, 20),
                    Cue.ReloadOut => .54f * Metal(time, high, 680),
                    Cue.ReloadIn => .72f * Metal(time, high, 940) + .16f * low * Fade(time, 30),
                    Cue.ReloadDone => .62f * Metal(time, high, 1290),
                    Cue.Brake => (0.32f * high + 0.13f * Wave(time, 630 - time * 270)) * Fade(time, 10),
                    Cue.PanelOpen => Tone(time, 480) + Tone(time - 0.032f, 680) * 0.68f,
                    Cue.PanelClose => Tone(time, 620) + Tone(time - 0.032f, 420) * 0.65f,
                    Cue.Loot => Tone(time, 720) + Tone(time - 0.08f, 980),
                    Cue.Sale => Tone(time, 1020) + Tone(time - 0.075f, 1350) + 0.53f * Tone(time - 0.15f, 1480),
                    Cue.LiftChime => .66f * Tone(time, 784) + .47f * Tone(time - .14f, 1046),
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
