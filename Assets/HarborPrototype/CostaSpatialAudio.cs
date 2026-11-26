using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaSpatialAudio
    {
        internal sealed class Voice
        {
            private readonly AudioSource source;
            private readonly AudioLowPassFilter filter;
            private float obstruction;
            private float targetObstruction;
            private float volume;
            private float sampleAt;

            internal Voice(Transform root, string name)
            {
                var host = new GameObject(name);
                host.transform.SetParent(root, false);
                source = host.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.dopplerLevel = 0;
                source.reverbZoneMix = .74f;
                filter = host.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 22000;
                filter.lowpassResonanceQ = 1;
                filter.enabled = false;
            }

            internal bool Playing => source.isPlaying;
            internal int Priority => source.priority;
            internal Vector3 Position => source.transform.position;
            internal void Stop() => source.Stop();

            internal void Play(AudioClip clip, Vector3 point, float gain, float near, float far, int priority,
                float pitch, bool loop, CostaSpatialAudio audio)
            {
                source.Stop();
                source.transform.position = point;
                source.clip = clip;
                source.loop = loop;
                source.pitch = pitch;
                source.priority = priority;
                source.minDistance = near;
                source.maxDistance = far;
                volume = gain;
                obstruction = targetObstruction = audio.Obstruction(point, far);
                sampleAt = Time.unscaledTime + .095f;
                Apply();
                source.Play();
            }

            internal void Move(Vector3 point, float gain, float pitch)
            {
                source.transform.position = point;
                volume = gain;
                source.pitch = pitch;
            }

            internal void Update(CostaSpatialAudio audio, float blend, float interval)
            {
                if (!Playing)
                    return;
                float now = Time.unscaledTime;
                if (now >= sampleAt)
                {
                    targetObstruction = audio.Obstruction(Position, source.maxDistance);
                    sampleAt = now + interval;
                }
                obstruction = Mathf.Lerp(obstruction, targetObstruction, blend);
                Apply();
            }

            private void Apply()
            {
                source.volume = volume * Mathf.Lerp(1, .32f, obstruction);
                bool obstructed = obstruction > .015f;
                if (filter.enabled != obstructed)
                    filter.enabled = obstructed;
                if (obstructed)
                    filter.cutoffFrequency = Mathf.Lerp(18000, 790, Mathf.Sqrt(obstruction));
            }
        }

        private readonly Voice[] transients;
        private readonly Transform root;
        private readonly Transform listener;
        private readonly AudioReverbZone reverb;
        private readonly CostaAcousticScene scene;
        private readonly CostaWorldDamage surfaces;
        private int next;
        private float enclosure;
        private float targetEnclosure;
        private float roomSampleAt;

        internal CostaSpatialAudio(Transform parent, Transform listener, CostaAcousticScene scene, CostaWorldDamage surfaces)
        {
            this.listener = listener;
            this.scene = scene;
            this.surfaces = surfaces;
            root = new GameObject("World sound").transform;
            root.SetParent(parent, false);
            reverb = new GameObject("Acoustic room").AddComponent<AudioReverbZone>();
            reverb.transform.SetParent(root, false);
            reverb.minDistance = .5f;
            reverb.maxDistance = 2f;
            reverb.reverbPreset = AudioReverbPreset.User;
            reverb.room = -10000;
            reverb.roomHF = -1050;
            reverb.decayHFRatio = .63f;
            reverb.reflections = -430;
            reverb.reflectionsDelay = .012f;
            reverb.reverb = 180;
            reverb.reverbDelay = .023f;
            reverb.diffusion = 91;
            reverb.enabled = false;

            transients = new Voice[Application.isMobilePlatform ? 10 : 16];
            for (int i = 0; i < transients.Length; i++)
                transients[i] = LoopVoice("Spatial effect " + (i + 1));
        }

        internal float Enclosure => enclosure;
        internal Vector3 Listener => listener.position;
        internal Voice LoopVoice(string name) => new(root, name);

        internal void Play(AudioClip clip, Vector3 point, float gain, float near, float far, int priority, float pitch = 1)
        {
            if (clip == null || gain <= 0 || (point - Listener).sqrMagnitude >= far * far)
                return;

            int slot = next;
            for (int i = 0; i < transients.Length; i++)
            {
                int index = (next + i) % transients.Length;
                if (!transients[index].Playing)
                {
                    slot = index;
                    break;
                }
                if (transients[index].Priority > transients[slot].Priority)
                    slot = index;
            }
            if (transients[slot].Playing && priority > transients[slot].Priority)
                return;

            transients[slot].Play(clip, point, gain, near, far, priority, pitch, false, this);
            next = (slot + 1) % transients.Length;
        }

        internal void Update(bool inWorld)
        {
            float dt = Mathf.Min(.10f, Time.unscaledDeltaTime);
            float blend = 1 - Mathf.Exp(-11 * dt);
            if (Time.unscaledTime >= roomSampleAt)
            {
                targetEnclosure = inWorld ? scene.Enclosure(Listener) : 0;
                roomSampleAt = Time.unscaledTime + .08f;
            }
            enclosure = Mathf.Lerp(enclosure, inWorld ? targetEnclosure : 0, 1 - Mathf.Exp(-5 * dt));
            bool reverberant = enclosure > .025f;
            if (reverb.enabled != reverberant)
                reverb.enabled = reverberant;
            if (reverberant)
            {
                reverb.transform.position = Listener;
                reverb.room = Mathf.RoundToInt(Mathf.Lerp(-7800, -685, enclosure));
                reverb.decayTime = Mathf.Lerp(.32f, 1.32f, enclosure);
            }
            foreach (Voice voice in transients)
                voice.Update(this, blend, Application.isMobilePlatform ? .20f : .13f);
        }

        private float Obstruction(Vector3 point, float far)
        {
            Vector3 delta = Listener - point;
            float length = delta.magnitude;
            if (length < .45f || length >= far)
                return 0;
            Vector3 direction = delta / length;
            float center = TransmissionLoss(point, direction, length);
            if (center == 0 || Application.isMobilePlatform)
                return center;
            Vector3 side = Vector3.Cross(direction, Vector3.up) * .23f;
            float left = TransmissionLoss(point - side, direction, length);
            float right = TransmissionLoss(point + side, direction, length);
            return .62f * center + .19f * (left + right);
        }

        private float TransmissionLoss(Vector3 start, Vector3 direction, float length)
        {
            if (!Physics.Raycast(start + direction * .13f, direction, out RaycastHit hit, length - .27f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return 0;
            return surfaces.Resolve(hit.collider) switch
            {
                CostaWorldDamage.Surface.Glass => .29f,
                CostaWorldDamage.Surface.Wood => .69f,
                CostaWorldDamage.Surface.Metal => .94f,
                _ => 1
            };
        }

        internal void Release() => Object.Destroy(root.gameObject);
    }
}
