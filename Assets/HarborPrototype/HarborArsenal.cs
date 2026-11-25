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

        internal static readonly string[] WeaponNames = { "Pistol", "Shotgun", "SMG", "Rifle", "Sniper" };
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
            new("Sniper", 4, 115, 1, 100, 1.05f, 0.002f, 1.8f, 27, false)
        };

        internal static (float range, float interval, int damage, int magazine, float reload) ShotRules(int index)
        {
            Weapon weapon = weaponRules[index];
            return (weapon.Range, weapon.Interval, weapon.Damage, weapon.Capacity, weapon.ReloadTime);
        }

        private readonly Weapon[] weapons = System.Array.ConvertAll(weaponRules, rule => rule.Copy());
        private readonly Transform mount;
        private readonly AudioSource audio;
        private readonly Transform flash;
        private readonly LineRenderer tracer;
        private readonly Material tracerMaterial;
        private readonly CostaJobs economy;
        private int slot = -1;
        private int heavy = -1;
        private int? onlineWeapons;
        private float nextShot;
        private float reloadedAt;
        private float flashUntil;
        private float hitUntil;

        private Weapon Current => weapons[slot];

        internal int Slot => slot;
        internal bool Armed => slot >= 0;
        internal bool Owned(int index)
        {
            return index >= 0 && index < weapons.Length
                && (onlineWeapons.HasValue ? (onlineWeapons.Value & (1 << index)) != 0 : economy.OwnsWeapon(index));
        }

        internal int Heavy => Owned(heavy) ? heavy : -1;

        internal void SetOnlineWeapons(int? mask)
        {
            if (onlineWeapons == mask)
                return;
            onlineWeapons = mask;
            if (!Owned(heavy))
                heavy = FirstHeavy();
            if (!Armed || !Owned(slot))
            {
                Select(-1);
                if (Owned(0)) Select(0);
                else SelectHeavy();
            }
        }
        internal int Loaded => Armed ? Current.Loaded : 0;
        internal int Capacity => Armed ? Current.Capacity : 0;
        internal float AimFov => Armed ? Current.AimFov : 66f;
        internal Vector3 ShotDirection { get; private set; }
        internal bool Reloading => reloadedAt > 0;
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
            return harbor.WeaponModel(parent, index, ModelLengths[index], ModelColors[index]);
        }

        internal void Reset()
        {
            foreach (Weapon weapon in weapons)
                weapon.Loaded = weapon.Capacity;
            if (Heavy < 0)
                heavy = FirstHeavy();
            nextShot = 0;
            hitUntil = 0;
            Select(-1);
            if (Owned(0)) Select(0);
            else SelectHeavy();
            audio.Stop();
        }

        internal void Select(int index)
        {
            if (index != -1 && !Owned(index))
                return;

            if (index > 0)
                heavy = index;
            slot = index;
            reloadedAt = 0;
            for (int i = 0; i < weapons.Length; i++)
                weapons[i].Model.gameObject.SetActive(i == slot);
            HideEffects();
        }

        internal void SelectHeavy()
        {
            if (Heavy >= 1 && Slot != heavy)
                Select(heavy);
        }

        private int FirstHeavy()
        {
            for (int i = 1; i < weapons.Length; i++)
                if (Owned(i))
                    return i;
            return -1;
        }

        internal void Cycle()
        {
            if (slot > 0 && Owned(0))
                Select(0);
            else
                SelectHeavy();
        }

        internal void SetVisible(bool visible)
        {
            mount.gameObject.SetActive(visible);
            if (!visible)
                HideEffects();
        }

        internal void Reload()
        {
            if (Armed && !Reloading && Current.Loaded < Current.Capacity)
                reloadedAt = Time.time + Current.ReloadTime;
        }

        internal bool Update(Camera camera, Vector3 hand, float pitch, bool triggerPressed, bool triggerHeld, bool aiming,
            Action<Collider, int> applyHit)
        {
            if (!Armed)
                return false;
            mount.SetPositionAndRotation(hand, mount.parent.rotation * Quaternion.Euler(pitch, 0, 0));
            if (flash.gameObject.activeSelf && Time.time >= flashUntil)
                HideEffects();

            if (Reloading && Time.time >= reloadedAt)
            {
                Current.Loaded = Current.Capacity;
                reloadedAt = 0;
            }

            Weapon weapon = Current;
            if (!(weapon.Automatic ? triggerHeld : triggerPressed) || Reloading || Time.time < nextShot)
                return false;
            if (weapon.Loaded == 0)
            {
                Reload();
                return false;
            }

            weapon.Loaded--;
            nextShot = Time.time + weapon.Interval;
            audio.PlayOneShot(weapon.Shot, 0.42f);

            Ray view = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            Vector3 muzzle = weapon.Muzzle.position;
            // Start collision checks at the shoulder so the visual barrel cannot bypass cover.
            Vector3 origin = mount.parent.position + Vector3.up * 1.22f;
            Vector3 aimPoint = Physics.Raycast(view, out RaycastHit aimHit, weapon.Range,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide)
                ? aimHit.point : view.GetPoint(weapon.Range);
            Vector3 forward = (aimPoint - origin).normalized;
            if (Vector3.Dot(forward, view.direction) <= 0.1f)
                forward = view.direction;

            ShotDirection = forward;
            Vector3 tracerEnd = origin + forward * weapon.Range;
            float spread = weapon.Spread * (aiming && weapon.Pellets == 1 ? 0.35f : 1);
            for (int i = 0; i < weapon.Pellets; i++)
            {
                Vector2 scatter = i == 0 ? Vector2.zero : UnityEngine.Random.insideUnitCircle;
                Vector3 direction = (forward + (camera.transform.right * scatter.x
                    + camera.transform.up * scatter.y) * spread).normalized;
                if (weapon.Pellets == 1)
                {
                    Vector2 sway = UnityEngine.Random.insideUnitCircle * spread;
                    direction = (forward + camera.transform.right * sway.x + camera.transform.up * sway.y).normalized;
                }

                if (Physics.Raycast(origin, direction, out RaycastHit hit, weapon.Range,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    applyHit(hit.collider, weapon.Damage);
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
                samples[i] = (burst * 0.48f + body * 0.34f) * Mathf.Min(1f, i / 9f);
            }
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
