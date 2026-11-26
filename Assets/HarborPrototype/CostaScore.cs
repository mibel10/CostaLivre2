using System.Collections;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaScore
    {
        private static readonly string[] Tracks = { "Shore", "Tension", "Pursuit", "Combat" };
        private readonly Transform root;
        private readonly AudioSource[] voices = new AudioSource[Tracks.Length];
        private float combatUntil;
        private bool ready;

        internal CostaScore(Transform root) => this.root = root;

        internal IEnumerator Load()
        {
            var loading = new ResourceRequest[Tracks.Length];
            for (int i = 0; i < Tracks.Length; i++)
                loading[i] = Resources.LoadAsync<AudioClip>("Harbor/Score/" + Tracks[i]);
            float deadline = Time.realtimeSinceStartup + 12;
            for (int i = 0; i < Tracks.Length; i++)
            {
                yield return loading[i];
                if (loading[i].asset is not AudioClip clip)
                    yield break;

                var source = new GameObject("Score " + Tracks[i]).AddComponent<AudioSource>();
                source.transform.SetParent(root, false);
                source.clip = clip;
                source.loop = true;
                source.playOnAwake = false;
                source.spatialBlend = 0;
                source.reverbZoneMix = 0;
                source.volume = 0;
                source.priority = 56;
                voices[i] = source;
                if (clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
                while (clip.loadState == AudioDataLoadState.Loading && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (clip.loadState != AudioDataLoadState.Loaded)
                    yield break;
            }

            double beat = AudioSettings.dspTime + .18;
            foreach (AudioSource voice in voices)
                voice.PlayScheduled(beat);
            ready = true;
        }

        internal void Combat(float duration = 3.4f) =>
            combatUntil = Mathf.Max(combatUntil, Time.unscaledTime + duration);

        internal void Reset() => combatUntil = 0;

        internal void Update(float volume, bool menu, int wanted, float danger, bool fight)
        {
            if (!ready)
                return;
            bool combat = !menu && (fight || Time.unscaledTime < combatUntil);
            float chase = menu ? 0 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 4, wanted));
            float tension = menu ? 0 : Mathf.Clamp01(Mathf.Max(danger, wanted * .29f));
            float output = Mathf.Clamp01(volume);
            Mix(0, output * (menu ? .42f : combat ? .48f : .70f));
            Mix(1, output * tension * (combat ? .49f : .68f));
            Mix(2, output * chase * (combat ? .49f : .77f));
            Mix(3, combat ? output * .69f : 0);
        }

        private void Mix(int index, float level)
        {
            AudioSource voice = voices[index];
            float seconds = level > voice.volume ? .62f : 1.65f;
            voice.volume = Mathf.Lerp(voice.volume, level, 1 - Mathf.Exp(-Time.unscaledDeltaTime / seconds));
        }
    }
}
