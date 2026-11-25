using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaRemoteView
    {
        private sealed class Rendered
        {
            internal HarborCharacter Body;
            internal CostaLook Look;
            internal GameObject Pack;
            internal Mesh[] UniformMeshes;
            internal Transform Mount;
            internal Transform Flash;
            internal AudioSource Audio;
            internal int Weapon = -1;
            internal int LastShot;
            internal float FlashUntil;
            internal float KnifeSwingUntil;
            internal CostaSession.Avatar State;
        }

        private readonly Dictionary<int, Rendered> models = new();
        private readonly List<int> stale = new();
        private readonly HarborBlockout art;
        private readonly Transform root;
        private GUIStyle nameTag;
        private GameObject[] weapons;
        private AudioClip[] shotClips;

        internal CostaRemoteView(HarborBlockout art, Transform root)
        {
            this.art = art;
            this.root = root;
        }

        internal void Refresh(CostaSession session, CostaVehicle[] cars)
        {
            foreach (CostaSession.Avatar avatar in session.Players)
            {
                if (avatar.id == session.Local.id || !avatar.playing)
                    continue;
                if (!models.TryGetValue(avatar.id, out Rendered model))
                {
                    HarborCharacter body = art.Person("Remote player", root, avatar.pos, Color.white);
                    model = new Rendered { Body = body, Look = new CostaLook(art, body), Pack = art.Pack(body.Root, body.Chest) };
                    model.Mount = new GameObject("Remote weapon mount").transform;
                    model.Mount.SetParent(body.Root, false);
                    model.Flash = art.WeaponFlash(model.Mount);
                    model.Audio = body.Root.gameObject.AddComponent<AudioSource>();
                    model.Audio.spatialBlend = 1f;
                    model.LastShot = avatar.shotNumber;
                    model.UniformMeshes = body.Root.GetComponentsInChildren<MeshFilter>(true)
                        .Where(filter => filter.name == "Front vest" || filter.name == "Back vest")
                        .Select(filter => filter.sharedMesh).ToArray();
                    models.Add(avatar.id, model);
                }
                model.State = avatar;
                model.Look.Apply(avatar.outfit, avatar.police);
                model.Pack.SetActive(avatar.wanted && !avatar.police);
                bool bike = avatar.vehicle >= 0 && avatar.vehicle < cars.Length && cars[avatar.vehicle].IsBike;
                model.Body.Root.gameObject.SetActive(avatar.vehicle < 0 || bike);

                if (bike)
                {
                    CostaVehicle car = cars[avatar.vehicle];
                    Quaternion rotation = car.Root.rotation * Quaternion.Euler(11, 0, car.RiderLean);
                    Vector3 seat = car.Root.TransformPoint(new Vector3(0, 0.85f, -0.27f));
                    model.Body.Root.SetPositionAndRotation(seat - rotation * Vector3.up * 0.94f, rotation);
                    model.Body.Ride();
                }
                else if (avatar.vehicle < 0)
                {
                    float amount = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 15f);
                    Transform body = model.Body.Root;
                    Vector3 position = Vector3.Distance(body.position, avatar.pos) > 10
                        ? avatar.pos : Vector3.Lerp(body.position, avatar.pos, amount);
                    body.SetPositionAndRotation(position,
                        Quaternion.Slerp(body.rotation, Quaternion.Euler(0, avatar.yaw, 0), amount));
                    if (avatar.rollPose >= 0)
                        model.Body.Animate(HarborCharacter.Motion.Roll, progress: avatar.rollPose);
                    else if (avatar.jumpPose >= 0)
                        model.Body.Animate(HarborCharacter.Motion.Jump, progress: avatar.jumpPose);
                    else
                        model.Body.Animate(avatar.moving ? (avatar.running ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Walk)
                            : HarborCharacter.Motion.Idle, HarborArsenal.GaitSpeed(avatar.equipped, avatar.running, false, avatar.crouched));
                    if (avatar.crouched)
                        model.Body.CrouchPose();
                    DrawWeapon(model, avatar);
                }
            }

            stale.Clear();
            foreach (var pair in models)
            {
                bool found = false;
                foreach (CostaSession.Avatar avatar in session.Players)
                    if (avatar.id == pair.Key && avatar.playing) { found = true; break; }
                if (!found) stale.Add(pair.Key);
            }
            foreach (int id in stale)
            {
                Release(models[id]);
                models.Remove(id);
            }
        }

        private void DrawWeapon(Rendered model, CostaSession.Avatar avatar)
        {
            if (model.Weapon != avatar.equipped)
            {
                model.Weapon = avatar.equipped;
                model.KnifeSwingUntil = 0;
                for (int i = model.Mount.childCount - 1; i >= 0; i--)
                {
                    Transform child = model.Mount.GetChild(i);
                    if (child == model.Flash)
                        continue;
                    child.gameObject.SetActive(false);
                    Object.Destroy(child.gameObject);
                }
                if (model.Weapon >= 0 && model.Weapon < HarborArsenal.WeaponNames.Length)
                {
                    EnsureWeaponTemplates();
                    Object.Instantiate(weapons[model.Weapon], model.Mount, false).SetActive(true);
                }
            }

            if (avatar.shotNumber != model.LastShot)
            {
                model.LastShot = avatar.shotNumber;
                if (avatar.shotWeapon >= 0 && avatar.shotWeapon < HarborArsenal.WeaponNames.Length)
                {
                    EnsureWeaponTemplates();
                    model.Audio.PlayOneShot(shotClips[avatar.shotWeapon], 0.42f);
                    if (avatar.shotWeapon == HarborArsenal.Knife)
                        model.KnifeSwingUntil = Time.unscaledTime + HarborArsenal.KnifeSwingTime;
                    else
                    {
                        model.Flash.localPosition = Vector3.forward * 0.50f;
                        model.FlashUntil = Time.unscaledTime + 0.055f;
                    }
                }
            }
            bool knife = model.Weapon == HarborArsenal.Knife;
            if (model.Weapon >= 0 && avatar.rollPose < 0)
            {
                if (knife)
                {
                    float swing = Time.unscaledTime < model.KnifeSwingUntil
                        ? 1 - (model.KnifeSwingUntil - Time.unscaledTime) / HarborArsenal.KnifeSwingTime : -1;
                    model.Body.HoldKnife(avatar.running && avatar.jumpPose < 0, avatar.crouched, swing);
                }
                else
                    model.Body.HoldWeapon(false, 0, false);
            }
            model.Mount.SetPositionAndRotation(model.Body.WeaponHand,
                model.Body.Root.rotation * Quaternion.Euler(knife ? 20f : 0f, 0, 0));
            model.Flash.gameObject.SetActive(Time.unscaledTime < model.FlashUntil);
        }

        private void EnsureWeaponTemplates()
        {
            if (weapons != null)
                return;
            weapons = new GameObject[HarborArsenal.WeaponNames.Length];
            shotClips = new AudioClip[weapons.Length];
            for (int i = 0; i < weapons.Length; i++)
            {
                GameObject template = new GameObject(HarborArsenal.WeaponNames[i]);
                template.transform.SetParent(root, false);
                template.SetActive(false);
                HarborArsenal.BuildModel(art, template.transform, i);
                weapons[i] = template;
                shotClips[i] = HarborArsenal.MakeShot(template.name, i);
            }
        }

        internal CostaSession.Avatar ArrestCandidate(Vector3 position)
        {
            CostaSession.Avatar closest = null;
            float range = 2.65f;
            foreach (Rendered model in models.Values)
            {
                CostaSession.Avatar other = model.State;
                if (other.police || other.vehicle >= 0 || other.held > 0 || other.arrestProtected)
                    continue;
                float separation = Vector3.Distance(other.pos, position);
                if (separation >= range
                    || Physics.Linecast(position + Vector3.up * 0.87f, other.pos + Vector3.up * 0.87f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                range = separation;
                closest = other;
            }
            return closest;
        }

        internal void DrawNames(Camera camera)
        {
            if (nameTag == null)
            {
                nameTag = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold,
                    richText = false, clipping = TextClipping.Clip
                };
            }

            foreach (Rendered model in models.Values)
            {
                var state = model.State;
                if (!state.standing || state.crouched || state.vehicle >= 0)
                    continue;
                Vector3 head = model.Body.Head.position;
                if ((head - camera.transform.position).sqrMagnitude > 72 * 72
                    || Physics.Linecast(camera.transform.position, head, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    continue;
                Vector3 view = camera.WorldToViewportPoint(head + Vector3.up * 0.36f);
                if (view.z <= 0 || view.x < 0.04f || view.x > 0.96f || view.y < 0.06f || view.y > 0.94f)
                    continue;

                string label = state.police ? L("[POLICE] ", "[POLÍCIA] ") + state.name : state.name;
                float width = Mathf.Max(63, nameTag.CalcSize(new GUIContent(label)).x + 14);
                Rect rect = new(view.x * Screen.width - width / 2, (1 - view.y) * Screen.height - 12, width, 22);
                Color before = GUI.color;
                GUI.color = new Color(0.055f, 0.075f, 0.083f, 0.86f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = before;
                nameTag.normal.textColor = state.police ? new Color(0.57f, 0.79f, 1f) : Color.white;
                GUI.Label(rect, label, nameTag);
            }
        }

        internal void Clear()
        {
            foreach (Rendered model in models.Values)
                Release(model);
            models.Clear();
        }

        internal void Dispose()
        {
            Clear();
            if (weapons == null)
                return;
            foreach (GameObject weapon in weapons)
                Object.Destroy(weapon);
            foreach (AudioClip shot in shotClips)
                Object.Destroy(shot);
        }

        private void Release(Rendered model)
        {
            model.Body.Release();
            foreach (Mesh mesh in model.UniformMeshes)
                art.ReleaseModel(mesh);
            Object.Destroy(model.Body.Root.gameObject);
        }
    }
}
