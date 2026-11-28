using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaCinematics
    {
        private readonly Camera camera;
        private readonly GameObject stage;
        private readonly PlayableDirector director;
        private readonly CinemachineBrain brain;
        private readonly AudioSource voice;
        private readonly float[] samples = new float[128];
        private CinemachineCamera[] cameras;
        private TimelineClip[] clips;
        private TimelineAsset timeline;
        private CinemachineTrack track;
        private CostaCinematic sequence;
        private HarborCharacter actor;
        private Collider actorCollider;
        private Pose anchor, gameplay;
        private float gameplayFov;
        private Action ended;
        private bool previousBrain;
        private float elapsed, mouth;
        private int activeLine = -1;
        private string subtitle;
        private GUIStyle caption, skip;
        internal bool Active => sequence != null;

        internal CostaCinematics(Camera camera, Transform parent)
        {
            this.camera = camera;
            stage = new GameObject("Cutscene Timeline");
            stage.transform.SetParent(parent, false);
            director = stage.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.Manual;
            director.extrapolationMode = DirectorWrapMode.None;
            brain = camera.GetComponent<CinemachineBrain>();
            if (brain == null)
            {
                brain = camera.gameObject.AddComponent<CinemachineBrain>();
                brain.enabled = false;
            }
            voice = stage.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0;
        }

        internal bool Play(CostaCinematic asset, HarborCharacter body, Collider collider, Action finish)
        {
            if (Active || asset == null || asset.shots == null || asset.shots.Length == 0 ||
                Array.Exists(asset.shots, shot => shot.seconds <= 0 || shot.fieldOfView <= 0))
                return false;
            sequence = asset;
            actor = body;
            actorCollider = collider;
            anchor = new Pose(body.Root.position, body.Root.rotation);
            gameplay = new Pose(camera.transform.position, camera.transform.rotation);
            gameplayFov = camera.fieldOfView;
            ended = finish;
            elapsed = mouth = 0;
            activeLine = -1;
            subtitle = "";
            previousBrain = brain.enabled;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            brain.enabled = true;
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = asset.name;
            track = timeline.CreateTrack<CinemachineTrack>(null, "Cutscene cameras");
            clips = new TimelineClip[asset.shots.Length + 2];
            cameras = new CinemachineCamera[clips.Length];
            MakeShot(0, 0, .72f, 0);
            double cursor = .72f;
            for (int i = 0; i < asset.shots.Length; i++)
            {
                var shot = asset.shots[i];
                double dissolve = Math.Min(i == 0 ? .52 : .9, Math.Max(0, shot.dissolve));
                dissolve = Math.Min(dissolve, clips[i].duration * .45);
                cursor -= dissolve;
                MakeShot(i + 1, cursor, shot.seconds, dissolve);
                cursor += shot.seconds;
            }

            MakeShot(clips.Length - 1, cursor - .65, 1.02, .65);
            Position(0, gameplay.position, gameplay.rotation, gameplayFov);
            Position(clips.Length - 1, gameplay.position, gameplay.rotation, gameplayFov);
            director.playableAsset = timeline;
            director.SetGenericBinding(track, brain);
            director.Play();
            Sample(0);
            return true;
        }

        private void MakeShot(int index, double start, double duration, double overlap)
        {
            GameObject shotObject = new GameObject($"Shot {index + 1}");
            shotObject.transform.SetParent(stage.transform, false);
            cameras[index] = shotObject.AddComponent<CinemachineCamera>();
            TimelineClip clip = track.CreateClip<CinemachineShot>();
            clips[index] = clip;
            clip.start = Math.Max(0, start);
            clip.duration = duration;
            clip.blendInDuration = overlap;
            if (index > 0)
                clips[index - 1].blendOutDuration = overlap;
            PropertyName key = new($"CostaShot{index}");
            ((CinemachineShot)clip.asset).VirtualCamera = new ExposedReference<CinemachineVirtualCameraBase> { exposedName = key };
            director.SetReferenceValue(key, cameras[index]);
        }

        internal void Tick()
        {
            if (!Active)
                return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true
                || Keyboard.current?.spaceKey.wasPressedThisFrame == true
                || Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            {
                Finish();
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= director.duration)
            {
                Finish();
                return;
            }
            Sample(elapsed);
        }

        private void Sample(float time)
        {
            for (int i = 0; i < sequence.shots.Length; i++)
            {
                if (time < clips[i + 1].start || time > clips[i + 1].end)
                    continue;
                var shot = sequence.shots[i];
                float t = Mathf.SmoothStep(0, 1, (time - (float)clips[i + 1].start) / shot.seconds);
                Vector3 focus = World(shot.lookAt);
                Vector3 eye = World(Vector3.Lerp(shot.start, shot.finish, t));
                Vector3 direction = eye - focus;
                if (direction.sqrMagnitude < .01f)
                    direction = -actor.Root.forward;
                if (Physics.Raycast(focus + direction.normalized * .23f, direction.normalized, out RaycastHit hit,
                    Mathf.Max(0, direction.magnitude - .23f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider != actorCollider)
                    eye = focus + direction.normalized * Mathf.Max(.36f, hit.distance + .04f);
                Position(i + 1, eye, Quaternion.LookRotation(focus - eye), shot.fieldOfView);
            }

            actor.Animate(HarborCharacter.Motion.Idle);
            int line = -1;
            if (sequence.lines != null)
                for (int i = 0; i < sequence.lines.Length; i++)
                {
                    var entry = sequence.lines[i];
                    if (time >= entry.start && time < entry.start + entry.seconds)
                    {
                        line = i;
                        break;
                    }
                }
            if (line != activeLine)
            {
                voice.Stop();
                activeLine = line;
                subtitle = "";
                actor.ClearExpression();
                if (line >= 0)
                {
                    var entry = sequence.lines[line];
                    subtitle = L(entry.english, entry.portuguese);
                    if (entry.voice != null)
                    {
                        voice.clip = entry.voice;
                        voice.time = Mathf.Min(entry.voice.length, time - entry.start);
                        voice.Play();
                    }
                }
            }
            if (line >= 0)
            {
                var entry = sequence.lines[line];
                float power = 0;
                if (voice.isPlaying)
                {
                    voice.GetOutputData(samples, 0);
                    for (int i = 0; i < samples.Length; i++)
                        power += samples[i] * samples[i];
                }
                mouth = Mathf.Lerp(mouth, Mathf.Clamp01(Mathf.Sqrt(power / samples.Length) * 7.5f),
                    1 - Mathf.Exp(-Time.unscaledDeltaTime * 33));
                actor.Speak(mouth, entry.expression, (time - entry.start) / entry.seconds, World(entry.gestureTarget));
            }

            director.time = time;
            director.Evaluate();
            brain.ManualUpdate();
        }

        private Vector3 World(Vector3 offset) => anchor.position + anchor.rotation * offset;

        private void Position(int index, Vector3 position, Quaternion rotation, float lens)
        {
            cameras[index].transform.SetPositionAndRotation(position, rotation);
            var settings = cameras[index].Lens;
            settings.FieldOfView = lens;
            settings.NearClipPlane = camera.nearClipPlane;
            settings.FarClipPlane = camera.farClipPlane;
            cameras[index].Lens = settings;
        }

        internal void SetFocused(bool focused)
        {
            if (!Active)
                return;
            if (focused)
                voice.UnPause();
            else
                voice.Pause();
        }

        internal void Draw()
        {
            if (!Active)
                return;
            Rect safe = Screen.safeArea;
            float scale = Mathf.Min(safe.width / 780f, safe.height / 520f);
            float top = Screen.height - safe.yMax;
            float band = 47 * scale;
            Color old = GUI.color;
            GUI.color = new Color(.025f, .038f, .044f, .86f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, top + band), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, Screen.height - safe.yMin - band * 1.4f, Screen.width, safe.yMin + band * 1.4f), Texture2D.whiteTexture);
            GUI.color = old;
            caption ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontStyle = FontStyle.Bold };
            caption.fontSize = Mathf.Max(14, Mathf.RoundToInt(scale * 20));
            if (!string.IsNullOrEmpty(subtitle))
                GUI.Label(new Rect(safe.x + safe.width * .10f, Screen.height - safe.yMin - band * 1.36f,
                    safe.width * .80f, band * 1.14f), subtitle, caption);
            skip ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter };
            skip.fontSize = Mathf.Max(12, Mathf.RoundToInt(scale * 14));
            if (CostaUi.Button(new Rect(safe.xMax - 127 * scale, top + 6 * scale, 113 * scale, 32 * scale),
                L("Skip", "Pular"), skip))
                Finish();
        }

        internal void Finish()
        {
            if (!Active)
                return;
            Action complete = ended;
            Release();
            camera.transform.SetPositionAndRotation(gameplay.position, gameplay.rotation);
            camera.fieldOfView = gameplayFov;
            complete?.Invoke();
        }

        internal void Release()
        {
            if (!Active)
                return;
            sequence = null;
            actor?.ClearExpression();
            actor = null;
            ended = null;
            voice.Stop();
            voice.clip = null;
            director.Stop();
            director.ClearGenericBinding(track);
            director.playableAsset = null;
            for (int i = 0; i < clips.Length; i++)
            {
                director.ClearReferenceValue(new PropertyName($"CostaShot{i}"));
                cameras[i].gameObject.SetActive(false);
                UnityEngine.Object.Destroy(cameras[i].gameObject);
                UnityEngine.Object.Destroy(clips[i].asset);
            }
            UnityEngine.Object.Destroy(track);
            UnityEngine.Object.Destroy(timeline);
            brain.enabled = previousBrain;
            clips = null;
            cameras = null;
            track = null;
            timeline = null;
        }
    }
}
