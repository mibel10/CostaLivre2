using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CostaLivre
{
    internal sealed class HarborCharacter
    {
        internal enum Motion { Idle, Walk, Run, Jump, Roll }

        internal sealed class Art
        {
            private static readonly string[] ClipNames = { "idle", "walk", "run", "jump", "roll" };
            internal readonly GameObject Model = Resources.Load<GameObject>("Harbor/Walker");
            internal readonly AnimationClip[] Clips = new AnimationClip[ClipNames.Length];

            internal Art()
            {
                foreach (AnimationClip clip in Resources.LoadAll<AnimationClip>("Harbor/Walker"))
                {
                    for (int i = 0; i < ClipNames.Length; i++)
                    {
                        if (clip.name.Equals(ClipNames[i], StringComparison.OrdinalIgnoreCase)
                            || clip.name.EndsWith("|" + ClipNames[i], StringComparison.OrdinalIgnoreCase))
                        {
                            Clips[i] = clip;
                        }
                    }
                }
                for (int i = 0; i < Clips.Length; i++)
                {
                    if (Clips[i] == null)
                        Debug.LogError($"Harbor character animation missing: {ClipNames[i]}");
                }
            }
        }

        internal readonly Transform Root;
        private readonly GameObject mesh;
        private readonly Renderer skin;
        private readonly AnimationClip[] clips;
        private readonly AnimationClipPlayable[] tracks;
        private readonly PlayableGraph graph;
        private readonly AnimationMixerPlayable mixer;
        private readonly Transform rightShoulder;
        private readonly Transform rightElbow;
        private readonly Transform rightHand;
        private readonly Transform leftShoulder;
        private readonly Transform leftElbow;
        private readonly Transform leftHand;
        private readonly Transform rightThigh;
        private readonly Transform rightCalf;
        private readonly Transform rightFoot;
        private readonly Transform leftThigh;
        private readonly Transform leftCalf;
        private readonly Transform leftFoot;
        private Motion activeMotion;
        private Motion previousMotion;
        private float blendRemaining;
        private const float BlendTime = 0.14f;
        private float clock;

        internal Vector3 WeaponHand => rightHand.position;
        internal Transform Head { get; }
        internal Transform Chest { get; }

        internal HarborCharacter(Art art, string name, Transform parent, Vector3 position, Material material)
        {
            Root = new GameObject(name).transform;
            Root.SetParent(parent, false);
            Root.position = position;
            mesh = UnityEngine.Object.Instantiate(art.Model, Root);
            mesh.name = "Walker rig";
            skin = mesh.GetComponentInChildren<SkinnedMeshRenderer>();
            Animator animator = mesh.GetComponent<Animator>();
            if (animator == null)
                animator = mesh.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            clips = art.Clips;
            graph = PlayableGraph.Create(name + " animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Character pose", animator);
            output.SetSourcePlayable(mixer);
            tracks = new AnimationClipPlayable[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    continue;
                AnimationClipPlayable track = AnimationClipPlayable.Create(graph, clips[i]);
                track.SetApplyFootIK(false);
                track.SetSpeed(0);
                graph.Connect(track, 0, mixer, i);
                tracks[i] = track;
            }
            mixer.SetInputWeight(0, 1);
            graph.Play();
            foreach (Renderer renderer in mesh.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = material;

            Transform[] bones = mesh.GetComponentsInChildren<Transform>();
            Transform foot = Find(bones, "foot_L");
            Transform toe = Find(bones, "toe_L");
            if (Vector3.Dot(toe.position - foot.position, Root.forward) < 0)
                mesh.transform.localRotation *= Quaternion.Euler(0, 180, 0);

            Head = Find(bones, "head");
            Chest = Find(bones, "chest");
            rightShoulder = Find(bones, "upperarm_R");
            rightElbow = Find(bones, "forearm_R");
            rightHand = Find(bones, "hand_R");
            leftShoulder = Find(bones, "upperarm_L");
            leftElbow = Find(bones, "forearm_L");
            leftHand = Find(bones, "hand_L");
            rightThigh = Find(bones, "thigh_R");
            rightCalf = Find(bones, "calf_R");
            rightFoot = Find(bones, "foot_R");
            leftThigh = Find(bones, "thigh_L");
            leftCalf = Find(bones, "calf_L");
            leftFoot = foot;
        }

        internal void Animate(Motion motion, float cycleSpeed = 1f, float progress = -1f)
        {
            if (motion != activeMotion)
            {
                if (blendRemaining > 0)
                    mixer.SetInputWeight((int)previousMotion, 0);
                bool sameGait = (activeMotion == Motion.Walk || activeMotion == Motion.Run)
                    && (motion == Motion.Walk || motion == Motion.Run);
                if (!sameGait)
                    clock = 0;
                previousMotion = activeMotion;
                activeMotion = motion;
                blendRemaining = progress < 0 && previousMotion <= Motion.Run && motion <= Motion.Run ? BlendTime : 0;
                mixer.SetInputWeight((int)previousMotion, blendRemaining > 0 ? 1 : 0);
                mixer.SetInputWeight((int)activeMotion, blendRemaining > 0 ? 0 : 1);
            }

            if (blendRemaining > 0)
            {
                blendRemaining = progress >= 0 ? 0 : Mathf.Max(0, blendRemaining - Time.unscaledDeltaTime);
                float weight = Mathf.SmoothStep(0, 1, blendRemaining / BlendTime);
                mixer.SetInputWeight((int)previousMotion, weight);
                mixer.SetInputWeight((int)activeMotion, 1f - weight);
            }

            AnimationClip clip = clips[(int)motion];
            if (clip == null)
                return;

            clock = Mathf.Repeat(clock + Time.unscaledDeltaTime * cycleSpeed / Mathf.Max(clip.length, 0.02f), 1f);
            float sample = progress < 0 ? clock : Mathf.Clamp01(progress);
            tracks[(int)motion].SetTime(sample * clip.length);
            graph.Evaluate(0);
            mesh.transform.localPosition = Vector3.zero;
        }

        internal void Release()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        internal void HoldWeapon(bool aiming, float pitch, bool support)
        {
            Vector3 pivot = new(0.25f, aiming ? 1.36f : 1.15f, aiming ? 0.34f : 0.28f);
            Vector3 lift = Quaternion.Euler(pitch * 0.65f, 0, 0) * Vector3.forward * 0.14f;
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(pivot + lift), Root.right);
            if (aiming && support)
            {
                Vector3 supportPoint = Root.TransformPoint(new Vector3(pivot.x - 0.12f, pivot.y - 0.025f, pivot.z + 0.27f) + lift);
                AlignArm(leftShoulder, leftElbow, leftHand, supportPoint, -Root.right);
            }
        }

        internal void HoldKnife(bool sprinting, bool crouched, float swing)
        {
            float stride = sprinting ? Mathf.Sin(clock * 2 * Mathf.PI) : 0;
            Vector3 right = new(0.31f, sprinting ? 1.03f : 1.17f, 0.19f + stride * 0.16f);
            if (swing >= 0)
            {
                float extension = Mathf.Sin(Mathf.PI * swing);
                right += new Vector3(Mathf.Lerp(0.15f, -0.45f, swing), 0.14f, 0.41f) * extension;
            }
            if (crouched)
                right.y -= 0.44f;
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(right), Root.right);
            if (sprinting)
            {
                Vector3 left = new(-0.28f, 1.18f + stride * 0.09f, 0.13f - stride * 0.27f);
                AlignArm(leftShoulder, leftElbow, leftHand, Root.TransformPoint(left), -Root.right);
            }
        }

        internal void WearTexture(Texture2D texture)
        {
            if (skin == null || texture == null)
                return;
            var properties = new MaterialPropertyBlock();
            skin.GetPropertyBlock(properties);
            properties.SetTexture("_BaseMap", texture);
            properties.SetFloat("_Smoothness", 0.19f);
            skin.SetPropertyBlock(properties);
        }

        internal void CrouchPose()
        {
            mesh.transform.localPosition = Vector3.down * 0.38f;
            AlignLimb(rightThigh, rightCalf, rightFoot, Root.TransformPoint(new Vector3(0.16f, 0.085f, -0.02f)), Root.forward);
            AlignLimb(leftThigh, leftCalf, leftFoot, Root.TransformPoint(new Vector3(-0.16f, 0.085f, -0.02f)), Root.forward);
        }

        internal void Ride()
        {
            Animate(Motion.Idle, progress: 0);
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(new Vector3(0.29f, 1.17f, 0.47f)), Root.right);
            AlignArm(leftShoulder, leftElbow, leftHand, Root.TransformPoint(new Vector3(-0.29f, 1.17f, 0.47f)), -Root.right);
            AlignLimb(rightThigh, rightCalf, rightFoot, Root.TransformPoint(new Vector3(0.26f, 0.41f, 0.04f)), Root.forward);
            AlignLimb(leftThigh, leftCalf, leftFoot, Root.TransformPoint(new Vector3(-0.26f, 0.41f, 0.04f)), Root.forward);
        }

        internal void VehiclePose(bool entering, float progress, float seated, int side, bool bike)
        {
            if (entering && progress < 0.29f)
            {
                Animate(Motion.Walk, progress: progress * 5f % 1f);
                return;
            }

            Animate(Motion.Idle, progress: 0);
            float knockPhase = Mathf.Clamp01((progress - 0.29f) / 0.22f);
            float knock = entering && !bike ? Mathf.Sin(Mathf.PI * knockPhase) : 0f;
            float brace = entering ? knock : Mathf.Sin(Mathf.PI * progress);
            mesh.transform.localPosition = Vector3.down * (seated * 0.38f + brace * 0.08f);

            Vector3 right = new(0.28f, 1.10f - seated * 0.23f, 0.20f + brace * 0.19f);
            Vector3 left = new(-0.28f, 1.02f - seated * 0.21f, 0.12f + seated * 0.20f);
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(right), Root.right);
            AlignArm(leftShoulder, leftElbow, leftHand, Root.TransformPoint(left), -Root.right);
            float step = Mathf.Sin(Mathf.PI * seated) * 0.17f;
            AlignLimb(rightThigh, rightCalf, rightFoot,
                Root.TransformPoint(new Vector3(0.15f + 0.10f * side * step, 0.06f + step, 0.05f + seated * 0.30f)), Root.forward);
            AlignLimb(leftThigh, leftCalf, leftFoot,
                Root.TransformPoint(new Vector3(-0.15f, 0.05f + step * 0.45f, -0.07f + seated * 0.32f)), Root.forward);
        }

        private void AlignArm(Transform shoulder, Transform elbow, Transform hand, Vector3 target, Vector3 outward)
        {
            AlignLimb(shoulder, elbow, hand, target, outward * 0.6f - Root.up);
        }

        private static void AlignLimb(Transform shoulder, Transform elbow, Transform hand, Vector3 target, Vector3 bendDirection)
        {
            Vector3 shoulderToTarget = target - shoulder.position;
            float upper = Vector3.Distance(shoulder.position, elbow.position);
            float lower = Vector3.Distance(elbow.position, hand.position);
            float distance = Mathf.Clamp(shoulderToTarget.magnitude, Mathf.Abs(upper - lower) + 0.01f, upper + lower - 0.01f);
            Vector3 axis = shoulderToTarget.normalized;
            Vector3 flex = Vector3.ProjectOnPlane(bendDirection, axis).normalized;
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            float bend = Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            Vector3 elbowPosition = shoulder.position + axis * along + flex * bend;
            shoulder.rotation = Quaternion.FromToRotation(elbow.position - shoulder.position, elbowPosition - shoulder.position)
                * shoulder.rotation;
            Vector3 extendedHand = shoulder.position + axis * distance;
            elbow.rotation = Quaternion.FromToRotation(hand.position - elbow.position, extendedHand - elbow.position)
                * elbow.rotation;
        }

        private static Transform Find(Transform[] bones, string name)
        {
            foreach (Transform bone in bones)
            {
                if (bone.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    return bone;
            }
            throw new InvalidOperationException($"Walker is missing the {name} bone.");
        }
    }
}
