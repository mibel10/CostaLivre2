using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace CostaLivre
{
    internal sealed class HarborCharacter
    {
        internal enum Motion { Idle, Walk, Run, Jump, Roll }
        internal enum Gesture { None, Reach, PickUp, OpenDoor, Impact }
        internal static float FallSeverity(float meters) => Mathf.InverseLerp(0.85f, 4.5f, meters);
        internal static float GestureDuration(Gesture gesture) => gesture switch
        {
            Gesture.OpenDoor => 0.64f, Gesture.PickUp => 0.52f, Gesture.Impact => 0.36f, _ => 0.42f
        };

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
        private AnimationMixerPlayable locomotionOutput;
        private AnimatorControllerPlayable locomotion;
        private static readonly int Lateral = Animator.StringToHash("Lateral");
        private static readonly int Forward = Animator.StringToHash("Forward");
        private static readonly int Stride = Animator.StringToHash("Stride");
        private float directionalBlend, gaitStride;
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
        private readonly float[] phases;
        private readonly float[] blendFrom;
        private Motion activeMotion;
        private float blend = 1;
        private float blendDuration;
        private float gaitSpeed = 1;
        private float turnLean;
        private Vector2 travelBlend;
        private float pivotTime, pivotAngle, pivotLength;
        private readonly Vector3[] contactPosition = new Vector3[2];
        private readonly bool[] heldContact = new bool[2];
        private float brakingPose;
        private float aimBlend;
        private float pitchLean;
        private float lateralLean;
        private float crouchDrop;
        private readonly float breathOffset;
        private readonly RaycastHit[] footHits = new RaycastHit[8];
        private readonly float[] footBlend = new float[2];
        private readonly float[] footHeightCorrection = new float[2];
        private readonly Vector3[] footNormal = { Vector3.up, Vector3.up };
        private readonly System.Collections.Generic.List<(Transform Anchor, Quaternion Neutral, float Response)> attachments = new();
        private float accessoryPitch;
        private float accessoryRoll;
        private float accessPitchVelocity;
        private float accessRollVelocity;
        private float previousYaw;
        private bool hasHeading;
        private float Clock => phases[(int)activeMotion];

        internal Vector3 WeaponHand => rightHand.position;
        internal Transform Head { get; }
        private readonly Transform jaw, leftEye, rightEye;
        private readonly Quaternion jawRest, leftEyeRest, rightEyeRest;
        internal Transform Chest { get; }

        internal HarborCharacter(Art art, string name, Transform parent, Vector3 position, Material material)
        {
            Root = new GameObject(name).transform;
            Root.SetParent(parent, false);
            Root.position = position;
            breathOffset = (Root.GetInstanceID() & 255) * 0.03f;
            mesh = UnityEngine.Object.Instantiate(art.Model, Root);
            mesh.name = "Walker rig";
            skin = mesh.GetComponentInChildren<SkinnedMeshRenderer>();
            Animator animator = mesh.GetComponent<Animator>();
            if (animator == null)
                animator = mesh.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            clips = art.Clips;
            graph = PlayableGraph.Create(name + " animation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Character pose", animator);
            output.SetSourcePlayable(mixer);
            tracks = new AnimationClipPlayable[clips.Length];
            phases = new float[clips.Length];
            blendFrom = new float[clips.Length];
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
            jaw = Array.Find(bones, bone => bone.name == "jaw");
            leftEye = Array.Find(bones, bone => bone.name == "eye_L");
            rightEye = Array.Find(bones, bone => bone.name == "eye_R");
            jawRest = jaw != null ? jaw.localRotation : Quaternion.identity;
            leftEyeRest = leftEye != null ? leftEye.localRotation : Quaternion.identity;
            rightEyeRest = rightEye != null ? rightEye.localRotation : Quaternion.identity;
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

        internal void AnimateDistance(Motion motion, float distanceSqr, float cycleSpeed = 1f, float progress = -1f)
        {
            if (motion == activeMotion && blend >= 1f)
            {
                if (distanceSqr > 45f * 45f && ((Time.frameCount + Root.GetInstanceID()) & 3) != 0)
                    return;
                if (distanceSqr > 22f * 22f && ((Time.frameCount + Root.GetInstanceID()) & 1) != 0)
                    return;
            }
            Animate(motion, cycleSpeed, progress);
        }

        internal void Animate(Motion motion, float cycleSpeed = 1f, float progress = -1f, bool snap = false)
        {
            int requested = (int)motion;
            if (!tracks[requested].IsValid())
                return;

            if (motion != activeMotion || snap)
            {
                bool sameGait = IsGait(activeMotion) && IsGait(motion);
                phases[requested] = sameGait && !snap ? Clock : 0;
                for (int i = 0; i < tracks.Length; i++)
                    blendFrom[i] = mixer.GetInputWeight(i);
                blendDuration = motion == Motion.Roll ? 0.055f : motion == Motion.Jump ? 0.085f :
                    activeMotion >= Motion.Jump ? 0.13f : 0.19f;
                activeMotion = motion;
                blend = snap ? 1 : 0;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.10f);
            if (snap)
            {
                aimBlend = crouchDrop = lateralLean = pitchLean = 0;
                footBlend[0] = footBlend[1] = 0;
                ClearFootContacts();
            }
            blend = Mathf.MoveTowards(blend, 1, dt / Mathf.Max(blendDuration, 0.01f));
            float eased = Mathf.SmoothStep(0, 1, blend);
            gaitSpeed = snap ? cycleSpeed : Mathf.MoveTowards(gaitSpeed, cycleSpeed, dt * 6f);
            for (int i = 0; i < tracks.Length; i++)
            {
                if (!tracks[i].IsValid())
                    continue;

                bool selected = i == requested;
                float weight = Mathf.Lerp(blendFrom[i], selected ? 1 : 0, eased);
                mixer.SetInputWeight(i, weight);
                if (!selected && weight <= 0)
                    continue;

                float duration = Mathf.Max(clips[i].length, 0.02f);
                if (selected && progress >= 0)
                    phases[i] = Mathf.Clamp01(progress);
                else if (i <= (int)Motion.Run)
                    phases[i] = Mathf.Repeat(phases[i] + dt * (i == (int)Motion.Idle ? 1 : gaitSpeed) / duration, 1);

                tracks[i].SetTime(phases[i] * duration);
            }

            if (locomotion.IsValid())
            {
                bool gait = IsGait(activeMotion);
                directionalBlend = Mathf.MoveTowards(directionalBlend, gait ? 1 : 0, dt * 9);
                locomotionOutput.SetInputWeight(0, 1 - directionalBlend);
                locomotionOutput.SetInputWeight(1, directionalBlend);
                if (gait || directionalBlend > 0)
                {
                    locomotion.SetFloat(Lateral, travelBlend.x);
                    locomotion.SetFloat(Forward, travelBlend.y);
                    gaitStride = Mathf.Repeat(gaitStride + dt * gaitSpeed / Mathf.Max(clips[requested].length, .12f), 1);
                    locomotion.SetFloat(Stride, gaitStride);
                }
            }
            graph.Evaluate(0);
            mesh.transform.localPosition = Vector3.zero;
            UpdateTurnLean(snap, dt);
            if (activeMotion < Motion.Jump)
            {
                float time = Time.unscaledTime + breathOffset;
                float breath = Mathf.Sin(time * 3.0f) * (activeMotion == Motion.Idle ? 0.7f : 0.37f);
                Chest.rotation = Quaternion.AngleAxis(breath, Root.right) * Chest.rotation;
                if (activeMotion == Motion.Idle)
                {
                    float glance = Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * 0.47f)), 7) * Mathf.Sin(time * 1.1f);
                    Head.rotation = Quaternion.AngleAxis(glance * 4f * (1 - aimBlend), Root.up) * Head.rotation;
                }
            }
        }

        internal void Speak(float mouth, float expression, float progress, Vector3 target)
        {
            if (jaw != null)
                jaw.rotation = Quaternion.AngleAxis(Mathf.Clamp01(mouth) * 20, Root.right) * jaw.parent.rotation * jawRest;
            float emphasis = Mathf.Sin(progress * Mathf.PI);
            Head.rotation = Quaternion.AngleAxis(expression * emphasis * 7 + mouth * 1.75f, Root.right) * Head.rotation;
            if (leftEye != null && rightEye != null)
            {
                Quaternion look = Quaternion.AngleAxis(emphasis * expression * 9, Root.up);
                leftEye.rotation = look * leftEye.parent.rotation * leftEyeRest;
                rightEye.rotation = look * rightEye.parent.rotation * rightEyeRest;
            }
            if (progress is > .08f and < .88f && target != Root.position)
                PlayGesture(Gesture.Reach, (progress - .08f) / .8f, target, .4f + .2f * expression);
        }

        internal void ClearExpression()
        {
            if (jaw != null) jaw.localRotation = jawRest;
            if (leftEye != null) leftEye.localRotation = leftEyeRest;
            if (rightEye != null) rightEye.localRotation = rightEyeRest;
        }

        internal void EnableDirectionalBlendTree()
        {
            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("Harbor/DirectionalGait");
            if (controller == null)
                return;
            locomotion = AnimatorControllerPlayable.Create(graph, controller);
            locomotionOutput = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(mixer, 0, locomotionOutput, 0);
            graph.Connect(locomotion, 0, locomotionOutput, 1);
            locomotionOutput.SetInputWeight(0, 1);
            var output = (AnimationPlayableOutput)graph.GetOutput(0);
            output.SetSourcePlayable(locomotionOutput);
        }

        internal void DirectionalGait(Vector3 velocity, float maximumSpeed, float turn, float braking)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, .08f);
            Vector3 local = Root.InverseTransformDirection(velocity);
            float speed = Mathf.Max(maximumSpeed, .01f);
            Vector2 input = Vector2.ClampMagnitude(new Vector2(local.x, local.z) / speed, 1);
            travelBlend = Vector2.Lerp(travelBlend, input, 1 - Mathf.Exp(-dt * 22));
            brakingPose = Mathf.Lerp(brakingPose, braking, 1 - Mathf.Exp(-dt * (braking > brakingPose ? 25 : 12)));

            if (Mathf.Abs(turn) >= 42 && pivotTime <= 0)
            {
                float angle = Mathf.Abs(turn);
                pivotAngle = Mathf.Sign(turn) * (angle < 68 ? 45 : angle < 135 ? 90 : 180);
                pivotLength = Mathf.Lerp(.19f, .33f, Mathf.Abs(pivotAngle) / 180);
                pivotTime = pivotLength;
            }

            if (IsGait(activeMotion) && !locomotion.IsValid())
            {
                float contactPhase = 2 * Mathf.PI * Clock;
                float stride = Mathf.Sin(contactPhase);
                float sideways = Mathf.Clamp(travelBlend.x * 2.5f, -1, 1);
                float forward = Mathf.Clamp(travelBlend.y * 2.5f, -1, 1);
                float strideYaw = Mathf.Atan2(sideways, .1f + Mathf.Abs(forward)) * Mathf.Rad2Deg * .40f;
                float reverse = Mathf.Max(0, -forward) * 2;
                leftThigh.rotation = Quaternion.AngleAxis(strideYaw, Root.up) *
                    Quaternion.AngleAxis(stride * (sideways * 11 + reverse * 15), Root.forward) * leftThigh.rotation;
                rightThigh.rotation = Quaternion.AngleAxis(strideYaw, Root.up) *
                    Quaternion.AngleAxis(-stride * (sideways * 11 - reverse * 15), Root.forward) * rightThigh.rotation;
            }

            Chest.rotation = Quaternion.AngleAxis(-brakingPose * 11, Root.right) * Chest.rotation;
            if (pivotTime <= 0)
                return;
            pivotTime = Mathf.Max(0, pivotTime - dt);
            float pose = Mathf.Sin(Mathf.PI * (1 - pivotTime / pivotLength));
            Transform lead = pivotAngle < 0 ? leftThigh : rightThigh;
            lead.rotation = Quaternion.AngleAxis(pivotAngle * pose * .19f, Root.up) *
                Quaternion.AngleAxis(-pose * 12, Root.right) * lead.rotation;
            Chest.rotation = Quaternion.AngleAxis(-pivotAngle * pose * .14f, Root.up) * Chest.rotation;
            mesh.transform.localPosition += Vector3.down * (pose * .036f);
        }

        internal void ClearFootContacts()
        {
            heldContact[0] = heldContact[1] = false;
            pivotTime = brakingPose = 0;
            travelBlend = Vector2.zero;
        }

        private static bool IsGait(Motion motion) => motion is Motion.Walk or Motion.Run;

        private void UpdateTurnLean(bool snap, float dt)
        {
            float yaw = Root.eulerAngles.y;
            float target = 0;
            if (hasHeading && !snap && IsGait(activeMotion) && dt > 0)
            {
                float rate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
                target = -Mathf.Clamp(rate / 32f, -1, 1) * (activeMotion == Motion.Run ? 8 : 4);
            }

            previousYaw = yaw;
            hasHeading = true;
            turnLean = snap ? 0 : Mathf.Lerp(turnLean, target, 1 - Mathf.Exp(-dt * 11f));
            if (activeMotion < Motion.Jump && Mathf.Abs(turnLean) > 0.05f)
                Chest.rotation = Quaternion.AngleAxis(turnLean, Root.forward) * Chest.rotation;
        }

        internal void Release()
        {
            if (graph.IsValid())
                graph.Destroy();
        }

        internal void BodyMotion(Vector3 velocity, Vector3 acceleration, bool grounded, bool crouched,
            float landing, float impact)
        {
            float dt = Mathf.Min(0.10f, Time.unscaledDeltaTime);
            Vector3 localVelocity = Root.InverseTransformDirection(velocity);
            Vector3 localAccel = Root.InverseTransformDirection(acceleration);
            float damping = 1 - Mathf.Exp(-dt * 12f);
            float twist = Mathf.Clamp(-localAccel.x * 0.28f, -9f, 9f);
            float drive = Mathf.Clamp(localVelocity.z * 0.88f + localAccel.z * 0.13f, -5, 10);
            lateralLean = Mathf.Lerp(lateralLean, grounded ? twist : twist * 0.42f, damping);
            pitchLean = Mathf.Lerp(pitchLean, drive, damping);
            crouchDrop = Mathf.MoveTowards(crouchDrop, crouched ? 0.38f : 0, dt * 2.8f);
            mesh.transform.localPosition = Vector3.down * (crouchDrop + landing * 0.24f);
            Chest.rotation = Root.rotation * Quaternion.Euler(pitchLean + landing * 19f - impact * 15f, 0, lateralLean) *
                Quaternion.Inverse(Root.rotation) * Chest.rotation;

            // Small secondary motion on rigid attachments, with a soft stop as the body settles.
            accessoryPitch = Mathf.SmoothDamp(accessoryPitch, Mathf.Clamp(-localAccel.z * 0.27f + landing * 10f, -12, 13),
                ref accessPitchVelocity, 0.115f, 150, dt);
            accessoryRoll = Mathf.SmoothDamp(accessoryRoll, lateralLean * -0.82f,
                ref accessRollVelocity, 0.14f, 130, dt);
            foreach (var item in attachments)
                item.Anchor.localRotation = item.Neutral * Quaternion.Euler(
                    accessoryPitch * item.Response, 0, accessoryRoll * item.Response);
        }

        internal void RegisterAttachment(Transform anchor, float response) =>
            attachments.Add((anchor, anchor.localRotation, response));

        internal void PlantFeet(Collider ownCollider = null)
        {
            PlantFoot(0, leftThigh, leftCalf, leftFoot, ownCollider);
            PlantFoot(1, rightThigh, rightCalf, rightFoot, ownCollider);
        }

        private void PlantFoot(int index, Transform thigh, Transform calf, Transform foot, Collider ownCollider)
        {
            const float sole = 0.077f;
            Vector3 animated = foot.position;
            float best = float.PositiveInfinity;
            RaycastHit selected = default;
            int count = Physics.RaycastNonAlloc(animated + Vector3.up * 0.37f, Vector3.down,
                footHits, 0.72f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = footHits[i];
                if (hit.collider == ownCollider || hit.normal.y < 0.67f || hit.distance >= best)
                    continue;
                selected = hit;
                best = hit.distance;
            }
            float influence = 0;
            Vector3 planarCorrection = Vector3.zero;
            float alpha = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 24f);
            if (best < float.PositiveInfinity)
            {
                Vector3 target = selected.point + selected.normal * sole;
                float gap = Vector3.Dot(animated - target, Vector3.up);
                // Only planted or penetrating feet are corrected. Preserve the swing of the other foot.
                influence = gap < 0 ? 1 : 1 - Mathf.InverseLerp(0.025f, 0.17f, gap);
                if (ownCollider != null && activeMotion < Motion.Jump)
                {
                    Vector3 separation = contactPosition[index] - target;
                    separation.y = 0;
                    if (!heldContact[index] || gap > .12f || separation.sqrMagnitude > .38f * .38f)
                    {
                        contactPosition[index] = target;
                        separation = Vector3.zero;
                        heldContact[index] = gap < .085f;
                    }
                    if (heldContact[index])
                        planarCorrection = Vector3.ClampMagnitude(separation, .28f);
                }
                float correction = Mathf.Clamp(target.y - animated.y, -0.18f, 0.27f);
                footHeightCorrection[index] = footBlend[index] < 0.03f ? correction
                    : Mathf.Lerp(footHeightCorrection[index], correction, alpha);
                footNormal[index] = Vector3.Slerp(footNormal[index], selected.normal, alpha);
            }
            else
                heldContact[index] = false;
            footBlend[index] = Mathf.Lerp(footBlend[index], influence, alpha);
            if (footBlend[index] > 0.025f)
            {
                Vector3 target = animated + (planarCorrection + Vector3.up * footHeightCorrection[index]) * footBlend[index];
                AlignLimb(thigh, calf, foot, target, Root.forward);
                foot.rotation = Quaternion.Slerp(Quaternion.identity,
                    Quaternion.FromToRotation(Vector3.up, footNormal[index]), footBlend[index]) * foot.rotation;
            }
        }

        internal void BraceLanding(float compression)
        {
            float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.26f, 0.82f, compression));
            if (weight <= 0.01f)
                return;

            Vector3 right = Root.TransformPoint(new Vector3(0.39f, 1.06f - compression * 0.24f, 0.44f));
            Vector3 left = Root.TransformPoint(new Vector3(-0.39f, 1.06f - compression * 0.24f, 0.44f));
            AlignArm(rightShoulder, rightElbow, rightHand, Vector3.Lerp(rightHand.position, right, weight), Root.right);
            AlignArm(leftShoulder, leftElbow, leftHand, Vector3.Lerp(leftHand.position, left, weight), -Root.right);
        }

        internal void PlayGesture(Gesture gesture, float progress, Vector3 target = default, float intensity = .5f)
        {
            if (gesture == Gesture.None || progress < 0 || progress > 1)
                return;
            float reach = Pulse(progress, 0, 1);
            bool pickup = gesture == Gesture.PickUp;
            float pitch = gesture == Gesture.Impact ? -Mathf.Lerp(5f, 24f, intensity) : pickup ? 17f : 6f;
            Chest.rotation = Quaternion.AngleAxis(pitch * reach, Root.right) * Chest.rotation;
            if (gesture == Gesture.Impact)
            {
                float bearing = target == default ? 0 : Root.InverseTransformPoint(target).normalized.x;
                Chest.rotation = Quaternion.AngleAxis(-bearing * Mathf.Lerp(4, 18, intensity) * reach, Root.forward)
                    * Chest.rotation;
                return;
            }

            Vector3 original = rightHand.position;
            Vector3 fallback = Root.TransformPoint(pickup ? new Vector3(0.22f, 0.76f, 0.42f)
                : new Vector3(0.28f, 1.14f, 0.49f));
            Vector3 handTarget = target == default ? fallback : target;
            if (pickup && progress > 0.51f)
                handTarget = Vector3.Lerp(handTarget, Root.TransformPoint(new Vector3(0.25f, 1.04f, 0.19f)),
                    Ease((progress - 0.51f) / 0.33f));
            AlignArm(rightShoulder, rightElbow, rightHand, Vector3.Lerp(original, handTarget, reach), Root.right);
            if (pickup)
                AlignArm(leftShoulder, leftElbow, leftHand,
                    Vector3.Lerp(leftHand.position, Root.TransformPoint(new Vector3(-0.21f, 0.93f, 0.27f)), reach * 0.52f),
                    -Root.right);
        }

        internal void HoldWeapon(bool aiming, float pitch, bool support)
        {
            float dt = Time.unscaledDeltaTime;
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1 : 0, dt * (aiming ? 8.3f : 6.8f));
            float weight = Ease(aimBlend);
            float phase = Clock * 2 * Mathf.PI;
            float sway = Mathf.Lerp(activeMotion == Motion.Run ? 0.049f : 0.025f, 0.007f, weight);
            Vector3 pivot = new(0.25f + Mathf.Sin(phase) * sway * 0.35f,
                Mathf.Lerp(1.15f, 1.36f, weight) + mesh.transform.localPosition.y + Mathf.Cos(phase * 2) * sway,
                Mathf.Lerp(0.28f, 0.34f, weight) + Mathf.Sin(phase) * sway * 0.4f);
            Quaternion angle = Quaternion.Euler(Mathf.Clamp(pitch, -45, 55), 0, 0);
            Vector3 lift = (angle * Vector3.forward - Vector3.forward) * 0.17f * weight;
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(pivot + lift), Root.right);
            if (support || weight > 0.025f)
            {
                Vector3 grip = support ? new Vector3(-0.074f, -0.07f, 0.29f) : new Vector3(-0.065f, -0.04f, 0.055f);
                Vector3 supportPoint = rightHand.position + Root.rotation * (angle * grip);
                AlignArm(leftShoulder, leftElbow, leftHand, Vector3.Lerp(leftHand.position, supportPoint,
                    support ? Mathf.Lerp(0.66f, 1, weight) : weight), -Root.right);
            }
        }

        internal Vector3 ReloadGrip(float amount)
        {
            Vector3 hold = Root.TransformPoint(new Vector3(.24f, 1.13f + mesh.transform.localPosition.y, .25f));
            Chest.rotation = Quaternion.AngleAxis(-8f * amount, Root.up) * Chest.rotation;
            AlignArm(rightShoulder, rightElbow, rightHand, Vector3.Lerp(rightHand.position, hold, amount), Root.right);
            return rightHand.position;
        }

        internal void ReloadSupport(Vector3 target, float weight) =>
            AlignArm(leftShoulder, leftElbow, leftHand, Vector3.Lerp(leftHand.position, target, weight), -Root.right);

        internal void HoldKnife(bool sprinting, float swing)
        {
            float stride = sprinting ? Mathf.Sin(Clock * 2 * Mathf.PI) : 0;
            Vector3 right = new(0.31f, sprinting ? 1.03f : 1.17f, 0.19f + stride * 0.16f);
            if (swing >= 0)
            {
                float extension = Mathf.Sin(Mathf.PI * swing);
                right += new Vector3(Mathf.Lerp(0.15f, -0.45f, swing), 0.14f, 0.41f) * extension;
            }
            right.y -= crouchDrop;
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(right), Root.right);
            if (sprinting)
            {
                Vector3 left = new(-0.28f, 1.18f + stride * 0.09f, 0.13f - stride * 0.27f);
                AlignArm(leftShoulder, leftElbow, leftHand, Root.TransformPoint(left), -Root.right);
            }
        }

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private MaterialPropertyBlock skinProperties;

        internal void WearTexture(Texture2D texture)
        {
            if (skin == null || texture == null)
                return;
            skinProperties ??= new MaterialPropertyBlock();
            skin.GetPropertyBlock(skinProperties);
            skinProperties.SetTexture(BaseMap, texture);
            // The clothing response texture already supplies per-pixel smoothness.
            skinProperties.SetFloat(Smoothness, 1f);
            skin.SetPropertyBlock(skinProperties);
        }

        internal void CrouchPose()
        {
            AlignLimb(rightThigh, rightCalf, rightFoot, Root.TransformPoint(new Vector3(0.16f, 0.085f, -0.02f)), Root.forward);
            AlignLimb(leftThigh, leftCalf, leftFoot, Root.TransformPoint(new Vector3(-0.16f, 0.085f, -0.02f)), Root.forward);
        }

        internal void Sit()
        {
            Animate(Motion.Idle, progress: 0, snap: true);
            mesh.transform.localPosition = Vector3.down * .53f;
            AlignLimb(rightThigh, rightCalf, rightFoot, Root.TransformPoint(new(.19f,.06f,.43f)), Root.forward);
            AlignLimb(leftThigh, leftCalf, leftFoot, Root.TransformPoint(new(-.19f,.06f,.43f)), Root.forward);
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(new(.21f,.59f,.24f)), Root.right);
            AlignArm(leftShoulder, leftElbow, leftHand, Root.TransformPoint(new(-.21f,.59f,.24f)), -Root.right);
        }

        internal void Ride()
        {
            Animate(Motion.Idle, progress: 0, snap: true);
            AlignArm(rightShoulder, rightElbow, rightHand, Root.TransformPoint(new Vector3(0.29f, 1.17f, 0.47f)), Root.right);
            AlignArm(leftShoulder, leftElbow, leftHand, Root.TransformPoint(new Vector3(-0.29f, 1.17f, 0.47f)), -Root.right);
            AlignLimb(rightThigh, rightCalf, rightFoot, Root.TransformPoint(new Vector3(0.26f, 0.41f, 0.04f)), Root.forward);
            AlignLimb(leftThigh, leftCalf, leftFoot, Root.TransformPoint(new Vector3(-0.26f, 0.41f, 0.04f)), Root.forward);
        }

        internal void VehiclePose(bool entering, float progress, float seated, int side, bool bike, Vector3? handle)
        {
            if (entering && progress < 0.29f)
            {
                Animate(Motion.Walk, progress: Mathf.Lerp(0.11f, 0.46f, progress / 0.29f));
                return;
            }

            Animate(Motion.Idle, progress: 0);
            float reach = entering ? Ease((progress - 0.26f) / 0.12f) * (1 - Ease((progress - 0.56f) / 0.16f))
                : 1 - Ease((progress - 0.31f) / 0.42f);
            float knock = entering && !bike ? Pulse(progress, 0.29f, 0.43f) : 0;
            float brace = entering ? Pulse(progress, 0.48f, 0.87f) : Pulse(progress, 0.14f, 0.84f);
            float lift = Mathf.Sin(seated * Mathf.PI);
            mesh.transform.localPosition = Vector3.down * (seated * 0.38f + brace * 0.10f);

            Quaternion shoulderTurn = Quaternion.AngleAxis(-side * (reach * 13f + brace * 5f), Root.up);
            Chest.rotation = Quaternion.AngleAxis(brace * 13f, Root.right) * shoulderTurn * Chest.rotation;

            float doorReach = bike ? 0.12f : 0.25f;
            Vector3 hand = new(0.28f, 1.10f - seated * 0.23f, 0.19f + reach * doorReach + knock * 0.13f);
            Vector3 support = new(0.28f, 1.02f - seated * 0.21f, 0.12f + seated * 0.19f + brace * 0.08f);
            Vector3 right = side > 0 ? hand : support;
            Vector3 left = side > 0 ? support : hand;
            left.x = -left.x;
            Vector3 rightTarget = Root.TransformPoint(right);
            Vector3 leftTarget = Root.TransformPoint(left);
            if (handle.HasValue)
            {
                float grip = reach * (1 - seated * 0.7f);
                if (side > 0)
                    rightTarget = Vector3.Lerp(rightTarget, handle.Value, grip);
                else
                    leftTarget = Vector3.Lerp(leftTarget, handle.Value, grip);
            }
            AlignArm(rightShoulder, rightElbow, rightHand, rightTarget, Root.right);
            AlignArm(leftShoulder, leftElbow, leftHand, leftTarget, -Root.right);

            float rightStep = lift * (side > 0 ? 0.24f : 0.09f);
            float leftStep = lift * (side > 0 ? 0.09f : 0.24f);
            AlignLimb(rightThigh, rightCalf, rightFoot,
                Root.TransformPoint(new Vector3(0.15f + rightStep * 0.15f, 0.065f + rightStep, 0.05f + seated * 0.30f)), Root.forward);
            AlignLimb(leftThigh, leftCalf, leftFoot,
                Root.TransformPoint(new Vector3(-0.15f - leftStep * 0.15f, 0.065f + leftStep, -0.07f + seated * 0.32f)), Root.forward);
        }

        private static float Ease(float value) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(value));

        private static float Pulse(float progress, float start, float end) =>
            Mathf.Sin(Mathf.PI * Mathf.InverseLerp(start, end, progress));

        private void AlignArm(Transform shoulder, Transform elbow, Transform hand, Vector3 target, Vector3 outward)
        {
            AlignLimb(shoulder, elbow, hand, target, outward * 0.6f - Root.up);
        }

        private static void AlignLimb(Transform shoulder, Transform elbow, Transform hand, Vector3 target, Vector3 bendDirection)
        {
            Vector3 shoulderToTarget = target - shoulder.position;
            float upper = Vector3.Distance(shoulder.position, elbow.position);
            float lower = Vector3.Distance(elbow.position, hand.position);
            if (upper < 0.015f || lower < 0.015f || shoulderToTarget.sqrMagnitude < 0.00001f)
                return;
            float distance = Mathf.Clamp(shoulderToTarget.magnitude, Mathf.Abs(upper - lower) + 0.005f, upper + lower - 0.005f);
            Vector3 axis = shoulderToTarget.normalized;
            Vector3 flex = Vector3.ProjectOnPlane(bendDirection, axis);
            if (flex.sqrMagnitude < 0.0001f)
                flex = Vector3.ProjectOnPlane(elbow.position - shoulder.position, axis);
            flex.Normalize();
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
