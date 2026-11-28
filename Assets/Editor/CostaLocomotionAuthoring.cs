using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CostaLivre.Editor
{
    internal static class CostaLocomotionAuthoring
    {
        private const string Model = "Assets/HarborPrototype/Resources/Harbor/Walker.fbx";
        private const string Output = "Assets/HarborPrototype/Resources/Harbor/DirectionalGait.controller";

        [MenuItem("Costa Livre/Animação/Reconstruir Blend Tree direcional")]
        public static void Rebuild()
        {
            AnimationClip[] clips = Resources.LoadAll<AnimationClip>("Harbor/Walker");
            AnimationClip Find(string name) => clips.Last(clip => clip.name.Equals(name, StringComparison.OrdinalIgnoreCase)
                || clip.name.EndsWith("|" + name, StringComparison.OrdinalIgnoreCase));
            AnimationClip walk = Find("walk");
            AnimationClip run = Find("run");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Output);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(Output);
            var machine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState previous in machine.states)
            {
                machine.RemoveState(previous.state);
                UnityEngine.Object.DestroyImmediate(previous.state, true);
            }
            foreach (var obsolete in AssetDatabase.LoadAllAssetsAtPath(Output))
                if (obsolete is BlendTree or AnimationClip)
                    UnityEngine.Object.DestroyImmediate(obsolete, true);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Lateral", AnimatorControllerParameterType.Float);
            controller.AddParameter("Forward", AnimatorControllerParameterType.Float);
            controller.AddParameter("Stride", AnimatorControllerParameterType.Float);
            AnimatorState state = machine.AddState("Directional locomotion");
            machine.defaultState = state;
            state.timeParameter = "Stride";
            state.timeParameterActive = true;
            state.iKOnFeet = false;

            var tree = new BlendTree
            {
                name = "World velocity relative to character",
                blendType = BlendTreeType.FreeformCartesian2D,
                blendParameter = "Lateral",
                blendParameterY = "Forward",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            state.motion = tree;
            tree.AddChild(walk, new Vector2(0, .48f));
            tree.AddChild(run, new Vector2(0, 1));
            tree.AddChild(Derive(walk, controller, "Side step left", -1, false), new Vector2(-.69f, .05f));
            tree.AddChild(Derive(walk, controller, "Side step right", 1, false), new Vector2(.69f, .05f));
            tree.AddChild(Derive(walk, controller, "Back step", 0, true), new Vector2(0, -.74f));
            EditorUtility.SetDirty(tree);
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Costa locomotion: 5 motion samples, including lateral and reverse cycles.");
        }

        private static AnimationClip Derive(AnimationClip source, AnimatorController controller, string name, float side, bool reverse)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(source);
            foreach (EditorCurveBinding binding in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
                if (reverse)
                {
                    Keyframe[] keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        Keyframe key = keys[i];
                        float input = key.inTangent;
                        key.inTangent = -key.outTangent;
                        key.outTangent = -input;
                        key.time = source.length - key.time;
                        (key.inWeight, key.outWeight) = (key.outWeight, key.inWeight);
                        keys[i] = key;
                    }
                    Array.Reverse(keys);
                    curve = new AnimationCurve(keys);
                }
                AnimationUtility.SetEditorCurve(clip, binding, curve);
            }

            if (side != 0)
                AuthorSideStep(source, clip, side);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            clip.EnsureQuaternionContinuity();
            AssetDatabase.AddObjectToAsset(clip, controller);
            return clip;
        }

        private static void AuthorSideStep(AnimationClip source, AnimationClip clip, float side)
        {
            GameObject rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            try
            {
                string[] names = { "thigh_L", "thigh_R", "calf_L", "calf_R", "foot_L", "foot_R" };
                Transform[] limbs = names.Select(name => rig.GetComponentsInChildren<Transform>()
                    .First(bone => bone.name == name)).ToArray();
                var curves = new AnimationCurve[limbs.Length, 4];
                for (int bone = 0; bone < limbs.Length; bone++)
                    for (int axis = 0; axis < 4; axis++)
                        curves[bone, axis] = new AnimationCurve();

                Vector3 forward = Vector3.ProjectOnPlane(rig.GetComponentsInChildren<Transform>()
                    .First(bone => bone.name == "toe_L").position - limbs[4].position, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                int frames = Mathf.Max(2, Mathf.CeilToInt(source.length * 30));
                var previous = new Quaternion[limbs.Length];
                for (int frame = 0; frame <= frames; frame++)
                {
                    float ratio = frame / (float)frames;
                    float clock = ratio * source.length;
                    source.SampleAnimation(rig, clock);
                    float cycle = Mathf.Sin(Mathf.PI * 2 * ratio + (side < 0 ? Mathf.PI : 0));
                    limbs[0].rotation = Quaternion.AngleAxis(-side * (13 + cycle * 21), forward) *
                        Quaternion.AngleAxis(side * 14, Vector3.up) * limbs[0].rotation;
                    limbs[1].rotation = Quaternion.AngleAxis(side * (13 - cycle * 21), forward) *
                        Quaternion.AngleAxis(side * 14, Vector3.up) * limbs[1].rotation;
                    limbs[4].rotation = Quaternion.AngleAxis(cycle * 8, right) * limbs[4].rotation;
                    limbs[5].rotation = Quaternion.AngleAxis(-cycle * 8, right) * limbs[5].rotation;
                    for (int bone = 0; bone < limbs.Length; bone++)
                    {
                        Quaternion pose = limbs[bone].localRotation;
                        if (frame > 0 && Quaternion.Dot(previous[bone], pose) < 0)
                            pose = new Quaternion(-pose.x, -pose.y, -pose.z, -pose.w);
                        previous[bone] = pose;
                        for (int axis = 0; axis < 4; axis++)
                            curves[bone, axis].AddKey(clock, pose[axis]);
                    }
                }
                var modified = new List<EditorCurveBinding>();
                var values = new List<AnimationCurve>();
                for (int bone = 0; bone < limbs.Length; bone++)
                {
                    string path = AnimationUtility.CalculateTransformPath(limbs[bone], rig.transform);
                    for (int axis = 0; axis < 4; axis++)
                    {
                        modified.Add(EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + "xyzw"[axis]));
                        values.Add(curves[bone, axis]);
                    }
                }
                AnimationUtility.SetEditorCurves(clip, modified.ToArray(), values.ToArray());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rig);
            }
        }
    }
}
