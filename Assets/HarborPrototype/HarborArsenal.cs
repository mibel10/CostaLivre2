using System;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class HarborArsenal : IDisposable
    {
        private sealed class Weapon
        {
            internal readonly string Name;
            internal readonly int Capacity;
            internal readonly int Damage;
            internal readonly int Pellets;
            internal readonly float Range;
            internal readonly float Interval;
            internal readonly float Spread;
            internal readonly float ReloadTime;
            internal readonly float AimFov;
            internal readonly bool Automatic;
            internal int Loaded;
            internal Transform Model;
            internal Transform Muzzle;
            internal AudioClip Shot;

            internal Weapon Copy() => (Weapon)MemberwiseClone();

            internal Weapon(string name, int capacity, int damage, int pellets, float range, float interval,
                float spread, float reloadTime, float aimFov, bool automatic)
            {
                Name = name;
                Capacity = capacity;
                Damage = damage;
                Pellets = pellets;
                Range = range;
                Interval = interval;
                Spread = spread;
                ReloadTime = reloadTime;
                AimFov = aimFov;
                Automatic = automatic;
            }
        }

        internal const int Knife = 5;
        internal const int KnifeMask = 1 << Knife;
        internal const float KnifeSwingTime = 0.33f;
        internal const float KnifeHitHeight = 1.13f;
        internal const float NormalSprintSpeed = 7.9f;
        internal const float NormalWalkSpeed = 4.15f;
        internal const float AimWalkSpeed = 2.65f;
        internal static readonly string[] WeaponNames = { "Pistol", "Shotgun", "SMG", "Rifle", "Sniper", "Knife" };
        private static readonly float[] ModelLengths = { 0.31f, 0.77f, 0.49f, 0.68f, 0.91f };
        private static readonly Color[] ModelColors =
        {
            new(0.27f, 0.29f, 0.30f), new(0.35f, 0.28f, 0.21f),
            new(0.26f, 0.29f, 0.29f), new(0.25f, 0.28f, 0.23f), new(0.27f, 0.30f, 0.32f)
        };

        private static readonly Weapon[] weaponRules =
        {
            new("Pistol", 12, 32, 1, 36, 0.25f, 0.009f, 1.05f, 53, false),
            new("Shotgun", 5, 19, 8, 19, 0.72f, 0.065f, 1.55f, 57, false),
            new("SMG", 24, 18, 1, 29, 0.082f, 0.028f, 1.35f, 54, true),
            new("Rifle", 20, 33, 1, 62, 0.155f, 0.016f, 1.55f, 46, true),
            new("Sniper", 4, 115, 1, 100, 1.05f, 0.002f, 1.8f, 27, false),
            new("Knife", 0, 40, 1, 1.75f, 0.55f, 0, 0, 66, false)
        };

        internal static (float range, float interval, int damage, int magazine, float reload, int pellets, float spread) ShotRules(int index)
        {
            Weapon weapon = weaponRules[index];
            return (weapon.Range, weapon.Interval, weapon.Damage, weapon.Capacity, weapon.ReloadTime, weapon.Pellets, weapon.Spread);
        }

        private readonly Weapon[] weapons = System.Array.ConvertAll(weaponRules, rule => rule.Copy());
        private readonly Transform mount;
        private readonly AudioSource audio;
        private readonly Transform flash;
        private readonly LineRenderer tracer;
        private readonly Material tracerMaterial;
        private readonly CostaJobs economy;
        private readonly CostaReloadMotion reloadMotion = new();
        private int slot = -1;
        private int heavy = -1;
        private int? onlineWeapons;
        private bool loanerPistol;
        private bool[] hostReloading;
        private float nextShot;
        private float reloadedAt;
        private float flashUntil;
        private float hitUntil;
        private float knifeSwingUntil;

        private Weapon Current => weapons[slot];

        internal int Slot => slot;
        internal bool Armed => slot >= 0;
        internal bool KnifeEquipped => slot == Knife;
        internal bool HasFirearm => Armed && !KnifeEquipped;
        internal float KnifeSwing => KnifeEquipped && Time.time < knifeSwingUntil
            ? 1f - (knifeSwingUntil - Time.time) / KnifeSwingTime : -1;
        internal static bool IsHeavy(int index) => index > 0 && index < Knife;
        internal static float SprintSpeed(int index) => index == Knife ? 9.2f : NormalSprintSpeed;
        internal static float WalkSpeed(int index, bool aiming) =>
            aiming ? (IsHeavy(index) ? 2.35f : AimWalkSpeed) : IsHeavy(index) ? 3.65f : NormalWalkSpeed;
        internal static float GaitSpeed(int index, bool running, bool aiming, bool crouched) =>
            running ? SprintSpeed(index) / NormalSprintSpeed : crouched ? 0.55f
                : aiming ? 0.79f * WalkSpeed(index, true) / AimWalkSpeed : WalkSpeed(index, false) / NormalWalkSpeed;
        internal bool Owned(int index)
        {
            return index == Knife || index >= 0 && index < Knife
                && (onlineWeapons.HasValue ? (onlineWeapons.Value & (1 << index)) != 0
                    : economy.OwnsWeapon(index) || index == 0 && loanerPistol);
        }

        internal int Heavy => Owned(heavy) ? heavy : FirstHeavy();

        internal void SetLoanerPistol(bool enabled)
        {
            if (loanerPistol == enabled)
                return;
            loanerPistol = enabled;
            if (enabled && !HasFirearm && Heavy < 0)
            {
                weapons[0].Loaded = weapons[0].Capacity;
                Select(0);
            }
            else if (Armed && !Owned(slot))
            {
                Select(Heavy >= 0 ? Heavy : Knife);
            }
        }

        internal void SetOnlineWeapons(int? mask)
        {
            if (onlineWeapons == mask)
                return;
            onlineWeapons = mask;
            if (!Owned(heavy))
                heavy = FirstHeavy();
            if (!Armed || !Owned(slot))
            {
                SelectDefault();
            }
        }
        internal int Loaded => Armed ? Current.Loaded : 0;
        internal int Capacity => Armed ? Current.Capacity : 0;
        internal float AimFov => Armed ? Current.AimFov : 66f;
        internal Vector3 ShotDirection { get; private set; }
        internal float ReloadProgress => HasFirearm && !onlineWeapons.HasValue && reloadedAt > Time.time
            ? Mathf.Clamp01(1 - (reloadedAt - Time.time) / Current.ReloadTime) : -1;
        internal bool Reloading => Armed && (onlineWeapons.HasValue ? hostReloading?[slot] == true : reloadedAt > 0);
        internal void SyncAmmo(int[] loaded, bool[] reloading)
        {
            hostReloading = reloading;
            for (int i = 0; i < weapons.Length; i++)
                weapons[i].Loaded = loaded[i];
        }
        internal bool HitVisible => Time.time < hitUntil;

        internal HarborArsenal(HarborBlockout harbor, Transform playerModel, Shader shader, CostaJobs economy)
        {
            this.economy = economy;
            mount = new GameObject("Weapon mount").transform;
            mount.SetParent(playerModel, false);
            mount.localPosition = new Vector3(0.31f, 1.24f, 0.22f);

            for (int i = 0; i < weapons.Length; i++)
            {
                Weapon weapon = weapons[i];
                weapon.Model = new GameObject(weapon.Name).transform;
                weapon.Model.SetParent(mount, false);
                weapon.Muzzle = BuildModel(harbor, weapon.Model, i);
                weapon.Shot = MakeShot(weapon.Name, i);
            }

            flash = harbor.WeaponFlash(mount);
            audio = mount.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            tracer = new GameObject("Shot tracer").AddComponent<LineRenderer>();
            tracer.transform.SetParent(mount, false);
            tracer.useWorldSpace = true;
            tracer.startWidth = 0.025f;
            tracer.endWidth = 0.012f;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            tracerMaterial = new Material(shader);
            tracerMaterial.color = HarborBlockout.Amber;
            tracerMaterial.EnableKeyword("_EMISSION");
            tracerMaterial.SetColor("_EmissionColor", new Color(1.5f, 0.73f, 0.21f));
            tracer.sharedMaterial = tracerMaterial;
            Reset();
        }

        internal static Transform BuildModel(HarborBlockout harbor, Transform parent, int index)
        {
            if (index == Knife)
            {
                Color grip = new(0.16f, 0.19f, 0.17f);
                harbor.Model(parent, "Knife grip", grip, new(-0.07f, 0.014f, -0.018f, 0.018f),
                    new(-0.05f, 0.019f, -0.022f, 0.022f), new(0.063f, 0.018f, -0.022f, 0.022f));
                harbor.Part(parent, PrimitiveType.Cube, "Knife guard", new(0, 0, 0.065f), new(0.061f, 0.048f, 0.018f), grip);
                harbor.Model(parent, "Knife blade", new Color(0.68f, 0.72f, 0.74f),
                    new(0.071f, 0.006f, -0.024f, 0.024f), new(0.22f, 0.005f, -0.024f, 0.019f),
                    new(0.31f, 0.001f, 0.006f, 0.008f));
                return parent;
            }
            return harbor.WeaponModel(parent, index, ModelLengths[index], ModelColors[index]);
        }

        internal void Reset()
        {
            foreach (Weapon weapon in weapons)
                weapon.Loaded = weapon.Capacity;
            nextShot = 0;
            hitUntil = 0;
            SelectDefault();
            audio.Stop();
        }

        internal void Select(int index)
        {
            if (index != -1 && !Owned(index))
                return;

            if (IsHeavy(index))
                heavy = index;
            slot = index;
            reloadMotion.Attach(index >= 0 && index < Knife ? weapons[index].Model : null, index);
            reloadedAt = 0;
            knifeSwingUntil = 0;
            for (int i = 0; i < weapons.Length; i++)
                weapons[i].Model.gameObject.SetActive(i == slot);
            HideEffects();
        }

        private void SelectDefault() => Select(Owned(0) ? 0 : Heavy >= 0 ? Heavy : Knife);

        internal void SelectHeavy()
        {
            int candidate = Heavy;
            if (candidate >= 1 && Slot != candidate)
                Select(candidate);
        }

        private int FirstHeavy()
        {
            for (int i = 1; i < Knife; i++)
                if (Owned(i))
                    return i;
            return -1;
        }

        internal void Cycle(int direction)
        {
            int current = slot == Knife ? 2 : IsHeavy(slot) ? 1 : 0;
            for (int offset = 1; offset < 3; offset++)
            {
                int index = (current + direction * offset + 3) % 3;
                int candidate = index == 0 ? 0 : index == 1 ? Heavy : Knife;
                if (Owned(candidate))
                {
                    Select(candidate);
                    return;
                }
            }
        }

        internal void SetVisible(bool visible)
        {
            mount.gameObject.SetActive(visible);
            if (!visible)
                HideEffects();
        }

        internal void Reload()
        {
            if (!onlineWeapons.HasValue && HasFirearm && !Reloading && Current.Loaded < Current.Capacity)
                reloadedAt = Time.time + Current.ReloadTime;
        }

        internal bool Update(Camera camera, HarborCharacter body, float pitch, bool triggerPressed, bool triggerHeld, bool aiming, bool crouching,
            Action<RaycastHit, int, bool> applyHit, CostaEffects effects, float onlineReload, bool canPoseReload)
        {
            if (!Armed)
                return false;
            float progress = onlineWeapons.HasValue ? onlineReload : ReloadProgress;
            if (!reloadMotion.Apply(canPoseReload ? progress : -1, body, mount, effects))
                mount.SetPositionAndRotation(body.WeaponHand, mount.parent.rotation * Quaternion.Euler(KnifeEquipped ? 20f : pitch, 0, 0));
            if (flash.gameObject.activeSelf && Time.time >= flashUntil)
                HideEffects();

            if (!onlineWeapons.HasValue && Reloading && Time.time >= reloadedAt)
            {
                Current.Loaded = Current.Capacity;
                reloadedAt = 0;
            }

            Weapon weapon = Current;
            if (!(weapon.Automatic ? triggerHeld : triggerPressed) || Reloading || Time.time < nextShot)
                return false;
            if (KnifeEquipped)
            {
                nextShot = Time.time + weapon.Interval;
                knifeSwingUntil = Time.time + KnifeSwingTime;
                audio.PlayOneShot(weapon.Shot, 0.42f);
                ShotDirection = mount.parent.forward;
                Vector3 knifeOrigin = mount.parent.position + Vector3.up * KnifeHitHeight;
                if (!onlineWeapons.HasValue && Physics.Raycast(knifeOrigin, ShotDirection, out RaycastHit strike, weapon.Range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    applyHit(strike, weapon.Damage, true);
                return true;
            }
            if (weapon.Loaded == 0)
            {
                Reload();
                return false;
            }

            if (!onlineWeapons.HasValue)
                weapon.Loaded--;
            nextShot = Time.time + weapon.Interval;
            audio.PlayOneShot(weapon.Shot, 0.42f);

            Ray view = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 muzzle = weapon.Muzzle.position;
            // Start collision checks at the shoulder so the visual barrel cannot bypass cover.
            Vector3 origin = mount.parent.position + Vector3.up * (crouching ? 0.90f : 1.22f);
            Vector3 aimPoint = Physics.Raycast(view, out RaycastHit aimHit, weapon.Range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                ? aimHit.point : view.GetPoint(weapon.Range);
            Vector3 forward = (aimPoint - origin).normalized;
            if (Vector3.Dot(forward, view.direction) <= 0.1f)
                forward = view.direction;

            ShotDirection = forward;
            Vector3 tracerEnd = origin + forward * weapon.Range;
            int visualPellets = onlineWeapons.HasValue ? 1 : weapon.Pellets;
            bool firstHit = true;
            for (int i = 0; i < visualPellets; i++)
            {
                Vector2 scatter = onlineWeapons.HasValue ? Vector2.zero : Scatter(i, weapon.Pellets, weapon.Spread, aiming);
                Vector3 direction = (forward + camera.transform.right * scatter.x + camera.transform.up * scatter.y).normalized;
                if (Physics.Raycast(origin, direction, out RaycastHit hit, weapon.Range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    if (!onlineWeapons.HasValue)
                        applyHit(hit, weapon.Damage, firstHit);
                    firstHit = false;
                    if (i == 0)
                        tracerEnd = hit.point;
                }
            }

            if (Vector3.Dot(tracerEnd - muzzle, forward) > 0.08f)
            {
                flash.position = muzzle;
                flash.gameObject.SetActive(true);
                tracer.SetPosition(0, muzzle);
                tracer.SetPosition(1, tracerEnd);
                tracer.enabled = true;
                flashUntil = Time.time + 0.055f;
            }
            return true;
        }

        internal static Vector2 Scatter(int pellet, int pellets, float spread, bool aiming) =>
            pellets > 1 && pellet == 0 ? Vector2.zero
                : UnityEngine.Random.insideUnitCircle * (aiming && pellets == 1 ? spread * 0.35f : spread);

        internal void ShowHit() => hitUntil = Time.time + 0.16f;

        internal void HideEffects()
        {
            flash.gameObject.SetActive(false);
            tracer.enabled = false;
        }

        public void Dispose()
        {
            foreach (Weapon weapon in weapons)
                UnityEngine.Object.Destroy(weapon.Shot);
            UnityEngine.Object.Destroy(tracerMaterial);
        }

        internal static AudioClip MakeShot(string name, int index)
        {
            const int rate = 22050;
            const int sampleCount = 2600;
            AudioClip clip = AudioClip.Create(name + " shot", sampleCount, 1, rate, false);
            float[] samples = new float[sampleCount];
            System.Random noise = new(43 + index);
            float tone = 135 - index * 12;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                float burst = (float)(noise.NextDouble() * 2 - 1) * Mathf.Exp(-t * (55 + index * 5));
                float body = Mathf.Sin(2 * Mathf.PI * (tone * t - 220 * t * t)) * Mathf.Exp(-t * 39);
                samples[i] = index == Knife
                    ? burst * 0.27f * Mathf.Sin(Mathf.PI * i / sampleCount)
                    : (burst * 0.48f + body * 0.34f) * Mathf.Min(1f, i / 9f);
            }
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
