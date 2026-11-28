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
            internal Vector3 Velocity;
            internal float AimPitch;
            internal HarborCharacter Body;
            internal CostaLook Look;
            internal GameObject Pack;
            internal Mesh[] UniformMeshes;
            internal Transform Mount;
            internal Transform Flash;
            internal int Weapon = -1;
            internal readonly CostaReloadMotion Reload = new();
            internal float ReloadProgress = -1;
            internal int LastShot;
            internal float FlashUntil;
            internal float KnifeSwingUntil;
            internal CostaSession.Avatar State;
            internal float SnapshotTime;
            internal Vector3 BlendPosition;
            internal Quaternion BlendRotation;
            internal float BlendStart;
            internal float BlendDuration;
            internal bool WasTransitioning;
            internal GUIContent Name = new();
            internal bool NameInPortuguese;
            internal float NameWidth;
            internal readonly GameObject[] WeaponModels = new GameObject[3];
            internal readonly int[] WeaponIds = { -1, -1, -1 };
            internal GameObject ActiveWeapon;
            internal readonly CostaVehicleMotion VehicleMotion = new();
            internal CostaEffects.Footfalls Footfalls;
        }

        private readonly Dictionary<int, Rendered> models = new();
        private readonly List<int> stale = new();
        private readonly Stack<Rendered> parked = new();
        private readonly Plane[] viewPlanes = new Plane[6];
        private readonly HarborBlockout art;
        private readonly CostaEffects effects;
        private readonly Transform root;
        private GUIStyle nameTag;
        private GameObject[] weapons;
        private AudioClip[] shotClips;

        internal CostaRemoteView(HarborBlockout art, Transform root, CostaEffects effects)
        {
            this.art = art;
            this.root = root;
            this.effects = effects;
        }

        internal void Refresh(CostaSession session, CostaVehicle[] cars, Camera camera)
        {
            float now = Time.unscaledTime;
            GeometryUtility.CalculateFrustumPlanes(camera, viewPlanes);
            foreach (CostaSession.Avatar avatar in session.Players)
            {
                if (avatar.id == session.Local.id || !avatar.playing)
                    continue;
                if (!models.TryGetValue(avatar.id, out Rendered model))
                {
                    model = parked.Count > 0 ? parked.Pop() : Create(avatar.pos);
                    model.LastShot = avatar.shotNumber;
                    models.Add(avatar.id, model);
                }
                CostaSession.Avatar previous = model.State;
                bool continuous = previous?.revision == avatar.revision;
                if (!continuous)
                {
                    model.VehicleMotion.Begin(avatar, cars);
                    model.Footfalls = default;
                    model.Velocity = Vector3.zero;
                    model.AimPitch = avatar.aimPitch;
                }
                if (previous != avatar)
                {
                    float cadence = Mathf.Clamp(now - model.SnapshotTime, .035f, .13f);
                    model.SnapshotTime = now;
                    if (!continuous || previous.pos != avatar.pos || previous.yaw != avatar.yaw)
                    {
                        model.BlendPosition = model.Body.Root.position;
                        model.BlendRotation = model.Body.Root.rotation;
                        model.BlendStart = now;
                        model.BlendDuration = continuous && (model.BlendPosition - avatar.pos).sqrMagnitude < 100 ? cadence : 0;
                    }
                    if (previous == null || previous.name != avatar.name || previous.police != avatar.police)
                        model.NameWidth = 0;
                }
                model.State = avatar;
                model.Look.Apply(avatar.outfit, avatar.police);
                model.Pack.SetActive(avatar.carryingLoot && !avatar.police);
                const float doorAudioRange = 31f;
                bool nearbyDoor = (avatar.doorCenter - camera.transform.position).sqrMagnitude < doorAudioRange * doorAudioRange;
                bool transitioning = model.VehicleMotion.Sample(model.Body, nearbyDoor ? effects : null);
                if (model.WasTransitioning && !transitioning)
                {
                    model.BlendPosition = model.Body.Root.position;
                    model.BlendRotation = model.Body.Root.rotation;
                    model.BlendStart = now;
                    model.BlendDuration = .075f;
                }
                model.WasTransitioning = transitioning;
                model.Mount.gameObject.SetActive(!transitioning && avatar.vehicle < 0 && avatar.seat < 0);
                if (transitioning)
                    continue;
                bool bike = avatar.vehicle >= 0 && avatar.vehicle < cars.Length && cars[avatar.vehicle].IsBike;
                bool openPassenger = avatar.vehicle >= 0 && avatar.vehicle < cars.Length && !bike && avatar.seatIndex >= 2;
                model.Body.Root.gameObject.SetActive(avatar.vehicle < 0 || bike || openPassenger);

                if (avatar.vehicle >= 0 && avatar.vehicle < cars.Length)
                {
                    CostaVehicle car = cars[avatar.vehicle];
                    PlayShot(model, avatar, camera.transform.position);
                    if (bike)
                    {
                        Quaternion rotation = car.Root.rotation * Quaternion.Euler(11, 0, car.RiderLean);
                        Vector3 seat = car.Root.TransformPoint(car.GetSeatLocalOffset(avatar.seatIndex));
                        model.Body.Root.SetPositionAndRotation(seat - rotation * Vector3.up * 0.94f, rotation);
                        if (InView(model.Body.Root.position))
                            model.Body.Ride();
                    }
                    else if (openPassenger)
                    {
                        Vector3 seat = car.Root.TransformPoint(car.GetSeatLocalOffset(avatar.seatIndex));
                        model.Body.Root.SetPositionAndRotation(seat - Vector3.up * 0.94f, Quaternion.Euler(0, avatar.yaw, 0));
                        if (InView(model.Body.Root.position))
                        {
                            model.Body.Animate(HarborCharacter.Motion.Idle);
                            DrawWeapon(model, avatar);
                        }
                    }
                }
                else if (avatar.vehicle < 0)
                {
                    float amount = model.BlendDuration <= 0 ? 1 : Mathf.Clamp01((now - model.BlendStart) / model.BlendDuration);
                    Transform body = model.Body.Root;
                    Vector3 before = body.position;
                    Vector3 position = Vector3.Lerp(model.BlendPosition, avatar.pos, amount);
                    body.SetPositionAndRotation(position,
                        Quaternion.Slerp(model.BlendRotation, Quaternion.Euler(0, avatar.yaw, 0), amount));
                    PlayShot(model, avatar, camera.transform.position);
                    if (continuous && (position - camera.transform.position).sqrMagnitude < 17f * 17f)
                    {
                        Vector3 moved = position - before;
                        bool onGround = avatar.jumpPose < 0 && avatar.rollPose < 0;
                        model.Footfalls.Update(effects, body, new Vector2(moved.x, moved.z).magnitude,
                            onGround && avatar.moving, avatar.running, avatar.crouched);
                        if (previous.jumpPose is >= 0 and < 0.78f && avatar.jumpPose >= 0.78f)
                            effects.PlayAt(CostaEffects.Cue.Land, position + Vector3.up * .20f, .26f);
                        if (previous.rollPose < 0 && avatar.rollPose >= 0)
                            effects.PlayAt(CostaEffects.Cue.Roll, position + Vector3.up * .24f, .28f);
                    }
                    Vector3 frameMovement = position - before;
                    frameMovement.y = 0;
                    float dt = Mathf.Max(0.001f, Time.unscaledDeltaTime);
                    Vector3 oldVelocity = model.Velocity;
                    Vector3 measured = continuous ? Vector3.ClampMagnitude(frameMovement / dt, 7.5f) : Vector3.zero;
                    model.Velocity = Vector3.Lerp(oldVelocity, measured, 1 - Mathf.Exp(-dt * 10f));
                    if (!InView(position))
                        continue;
                    if (avatar.seat >= 0)
                    {
                        model.Body.Sit();
                        continue;
                    }
                    if (avatar.rollPose >= 0)
                        model.Body.Animate(HarborCharacter.Motion.Roll, progress: avatar.rollPose, snap: !continuous);
                    else if (avatar.jumpPose >= 0)
                        model.Body.Animate(HarborCharacter.Motion.Jump, progress: avatar.jumpPose, snap: !continuous);
                    else
                        model.Body.Animate(avatar.moving ? (avatar.running ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Walk)
                            : HarborCharacter.Motion.Idle, HarborArsenal.GaitSpeed(avatar.equipped, avatar.running, avatar.aiming, avatar.crouched), snap: !continuous);
                    bool grounded = avatar.jumpPose < 0 || avatar.jumpPose >= 0.78f;
                    grounded = grounded && avatar.rollPose < 0;
                    if (avatar.rollPose < 0)
                        model.Body.BodyMotion(model.Velocity, (model.Velocity - oldVelocity) / dt,
                            grounded, avatar.crouched, avatar.landingCompression, 0);
                    if (avatar.crouched)
                        model.Body.CrouchPose();
                    if (grounded && (position - camera.transform.position).sqrMagnitude < 26 * 26)
                        model.Body.PlantFeet();
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
                Park(models[id]);
                models.Remove(id);
            }
        }

        private Rendered Create(Vector3 position)
        {
            HarborCharacter body = art.Person("Remote player", root, position, Color.white);
            var model = new Rendered { Body = body, Look = new CostaLook(art, body), Pack = art.Pack(body.Root, body.Chest) };
            body.RegisterAttachment(model.Pack.transform, 1f);
            model.Mount = new GameObject("Remote weapon mount").transform;
            model.Mount.SetParent(body.Root, false);
            model.Flash = art.WeaponFlash(model.Mount);
            model.UniformMeshes = body.Root.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.name == "Front vest" || filter.name == "Back vest")
                .Select(filter => filter.sharedMesh).ToArray();
            return model;
        }

        private bool InView(Vector3 position) =>
            GeometryUtility.TestPlanesAABB(viewPlanes, new Bounds(position + Vector3.up, new Vector3(3, 4, 3)));

        private void PlayShot(Rendered model, CostaSession.Avatar avatar, Vector3 listener)
        {
            if (avatar.shotNumber != model.LastShot)
            {
                model.LastShot = avatar.shotNumber;
                if (avatar.shotWeapon >= 0 && avatar.shotWeapon < HarborArsenal.WeaponNames.Length
                    && (model.Body.Root.position - listener).sqrMagnitude < 93f * 93f)
                {
                    EnsureWeaponTemplates();
                    effects.PlayShot(shotClips[avatar.shotWeapon], model.Body.Root.position + Vector3.up * 1.22f, .42f,
                        avatar.shotWeapon == HarborArsenal.Knife);
                    if (avatar.shotWeapon == HarborArsenal.Knife)
                        model.KnifeSwingUntil = Time.unscaledTime + HarborArsenal.KnifeSwingTime;
                    else
                    {
                        model.Flash.localPosition = Vector3.forward * 0.50f;
                        model.FlashUntil = Time.unscaledTime + 0.055f;
                    }
                }
            }
        }

        private void DrawWeapon(Rendered model, CostaSession.Avatar avatar)
        {
            if (model.Weapon != avatar.equipped)
            {
                model.Weapon = avatar.equipped;
                model.Reload.Attach(null);
                model.ReloadProgress = -1;
                if (model.ActiveWeapon != null)
                {
                    model.ActiveWeapon.SetActive(false);
                    model.ActiveWeapon = null;
                }
                if (model.Weapon >= 0 && model.Weapon < HarborArsenal.WeaponNames.Length)
                {
                    EnsureWeaponTemplates();
                    int slot = model.Weapon == HarborArsenal.Knife ? 2 : HarborArsenal.IsHeavy(model.Weapon) ? 1 : 0;
                    if (model.WeaponIds[slot] != model.Weapon)
                    {
                        Object.Destroy(model.WeaponModels[slot]);
                        model.WeaponModels[slot] = Object.Instantiate(weapons[model.Weapon], model.Mount, false);
                        model.WeaponIds[slot] = model.Weapon;
                    }
                    model.ActiveWeapon = model.WeaponModels[slot];
                    model.ActiveWeapon.SetActive(true);
                    model.Reload.Attach(model.ActiveWeapon.transform, model.Weapon);
                }
            }

            bool knife = model.Weapon == HarborArsenal.Knife;
            model.AimPitch = Mathf.Lerp(model.AimPitch, avatar.aimPitch, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 17f));
            if (model.Weapon >= 0 && avatar.rollPose < 0)
            {
                if (knife)
                {
                    float swing = Time.unscaledTime < model.KnifeSwingUntil
                        ? 1 - (model.KnifeSwingUntil - Time.unscaledTime) / HarborArsenal.KnifeSwingTime : -1;
                    model.Body.HoldKnife(avatar.running && avatar.jumpPose < 0, swing);
                }
                else
                    model.Body.HoldWeapon(avatar.aiming, model.AimPitch, HarborArsenal.IsHeavy(avatar.equipped));
            }
            model.Body.BraceLanding(avatar.rollPose < 0 ? avatar.landingCompression : 0);
            var gesture = (HarborCharacter.Gesture)avatar.gesture;
            if (avatar.rollPose < 0 && (gesture == HarborCharacter.Gesture.Impact || !avatar.aiming && avatar.jumpPose < 0))
                model.Body.PlayGesture(gesture, avatar.gestureProgress, avatar.gestureTarget, avatar.impactStrength);
            if (avatar.reloadProgress >= 0 && model.Weapon >= 0 && !knife && avatar.rollPose < 0)
                model.ReloadProgress = model.ReloadProgress < 0 ? avatar.reloadProgress : Mathf.Min(
                    avatar.reloadProgress + .075f, Mathf.Max(avatar.reloadProgress, model.ReloadProgress +
                    Time.unscaledDeltaTime / HarborArsenal.ShotRules(model.Weapon).reload));
            else
                model.ReloadProgress = -1;
            if (!model.Reload.Apply(model.ReloadProgress, model.Body, model.Mount, effects))
                model.Mount.SetPositionAndRotation(model.Body.WeaponHand,
                    model.Body.Root.rotation * Quaternion.Euler(knife ? 20f : model.AimPitch, 0, 0));
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
                if (other.police || other.vehicle >= 0 || other.doorTime > 0 || other.held > 0
                    || other.rollPose >= 0 || other.arrestProtected)
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
                if (!state.standing || state.crouched || state.vehicle >= 0 || !InView(model.Body.Root.position))
                    continue;
                Vector3 head = model.Body.Head.position;
                if ((head - camera.transform.position).sqrMagnitude > 72 * 72
                    || Physics.Linecast(camera.transform.position, head, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    continue;
                Vector3 view = camera.WorldToViewportPoint(head + Vector3.up * 0.36f);
                if (view.z <= 0 || view.x < 0.04f || view.x > 0.96f || view.y < 0.06f || view.y > 0.94f)
                    continue;

                bool inPortuguese = Portuguese;
                if (model.NameWidth <= 0 || model.NameInPortuguese != inPortuguese)
                {
                    model.NameInPortuguese = inPortuguese;
                    model.Name.text = state.police ? L("[POLICE] ", "[POLÍCIA] ") + state.name : state.name;
                    model.NameWidth = Mathf.Max(63, nameTag.CalcSize(model.Name).x + 14);
                }
                float width = model.NameWidth;
                Rect rect = new(view.x * Screen.width - width / 2, (1 - view.y) * Screen.height - 12, width, 22);
                Color before = GUI.color;
                GUI.color = new Color(0.055f, 0.075f, 0.083f, 0.86f);
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = before;
                nameTag.normal.textColor = state.police ? new Color(0.57f, 0.79f, 1f) : Color.white;
                GUI.Label(rect, model.Name, nameTag);
            }
        }

        internal void Clear()
        {
            foreach (Rendered model in models.Values)
                Park(model);
            models.Clear();
        }

        internal void Dispose()
        {
            Clear();
            while (parked.Count > 0)
                Release(parked.Pop());
            if (weapons == null)
                return;
            foreach (GameObject weapon in weapons)
                Object.Destroy(weapon);
            foreach (AudioClip shot in shotClips)
                Object.Destroy(shot);
        }

        private void Park(Rendered model)
        {
            model.VehicleMotion.Clear();
            model.Reload.Attach(null);
            if (model.ActiveWeapon != null)
                model.ActiveWeapon.SetActive(false);
            model.ActiveWeapon = null;
            model.Weapon = -1;
            model.ReloadProgress = -1;
            model.Flash.gameObject.SetActive(false);
            model.FlashUntil = model.KnifeSwingUntil = 0;
            model.WasTransitioning = false;
            model.Body.Root.gameObject.SetActive(false);
            model.State = null;
            parked.Push(model);
        }

        private void Release(Rendered model)
        {
            model.VehicleMotion.Clear();
            model.Body.Release();
            foreach (Mesh mesh in model.UniformMeshes)
                art.ReleaseModel(mesh);
            Object.Destroy(model.Body.Root.gameObject);
        }
    }
}
