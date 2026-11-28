using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed partial class CostaVehicle
    {
        private Renderer[] glassRenderers;
        private Transform fuelFlame;
        private int fuelTankDamage;
        internal int TireMask { get; private set; }
        internal int GlassMask { get; private set; }
        internal bool FuelBurning { get; private set; }
        internal float FuelBurnTimer { get; private set; }
        internal bool Wrecked { get; private set; }
        internal int SeatCount => IsBike ? 2 : 4;

        private void InitCombat(Renderer[] renderers)
        {
            glassRenderers = System.Array.FindAll(renderers, r =>
                r != null && (r.name == "Glass cabin" || r.name == "Swept glazing" || r.name == "Glazing" || r.name == "Windshield"));
        }

        internal Vector3 GetSeatLocalOffset(int seatIndex)
        {
            if (IsBike)
                return seatIndex == 1 ? new Vector3(0f, 0.90f, -0.78f) : new Vector3(0f, 0.85f, -0.27f);

            return seatIndex switch
            {
                1 => new Vector3(0.48f, 0.92f, 0.12f),     // Front passenger
                2 => new Vector3(-0.36f, 1.38f, -0.42f),   // Rear / sunroof shooter
                3 => new Vector3(0.44f, 1.05f, -0.92f),    // Rear-right / bed shooter
                _ => new Vector3(-0.48f, 0.92f, 0.12f)     // Driver
            };
        }

        internal float CombatTopSpeedFactor
        {
            get
            {
                if (Wrecked) return 0f;
                int burst = CountBits(TireMask);
                return Mathf.Max(0.38f, 1f - 0.14f * burst);
            }
        }

        internal float CombatGripFactor
        {
            get
            {
                int burst = CountBits(TireMask);
                return Mathf.Max(0.28f, 1f - 0.22f * burst);
            }
        }

        internal float CombatSteerDrift
        {
            get
            {
                if (IsBike || TireMask == 0) return 0f;
                int leftBurst = 0, rightBurst = 0;
                for (int i = 0; i < wheels.Length; i++)
                {
                    if ((TireMask & (1 << i)) == 0) continue;
                    if (wheelRest[i].x < 0f) leftBurst++; else rightBurst++;
                }
                return (leftBurst - rightBurst) * 14f;
            }
        }

        internal bool TryHitLocalized(Vector3 worldHitPoint, int damage, CostaEffects effects, out string partHit)
        {
            partHit = null;
            if (Wrecked)
                return false;

            Vector3 local = shell.InverseTransformPoint(worldHitPoint);

            // 1. Check Tires
            for (int i = 0; i < wheels.Length; i++)
            {
                if ((TireMask & (1 << i)) != 0)
                    continue;
                if ((local - wheelRest[i]).sqrMagnitude <= 0.52f * 0.52f)
                {
                    TireMask |= 1 << i;
                    ApplyWheelVisual(i, true);
                    effects?.PlayAt(CostaEffects.Cue.MetalHit, worldHitPoint, 0.55f);
                    effects?.SurfaceHit(CostaWorldDamage.Surface.Metal, worldHitPoint, (worldHitPoint - Root.position).normalized, true, worldHitPoint);
                    partHit = L("Tire burst!", "Pneu furado!");
                    return true;
                }
            }

            // 2. Check Windows / Glass (cars)
            if (!IsBike && local.y > 0.80f && local.z > -1.45f && local.z < 1.25f)
            {
                int zone = local.z > 0.35f ? 0 : (local.x < 0f ? 1 : 2);
                if ((GlassMask & (1 << zone)) == 0)
                {
                    GlassMask |= 1 << zone;
                    ApplyGlassVisual();
                    effects?.PlayAt(CostaEffects.Cue.Glass, worldHitPoint, 0.65f);
                    effects?.SurfaceHit(CostaWorldDamage.Surface.Glass, worldHitPoint, (worldHitPoint - Root.position).normalized, true, worldHitPoint);
                    partHit = zone == 0 ? L("Windshield shattered!", "Para-brisa quebrado!")
                        : L("Window shattered!", "Vidro quebrado!");
                    return true;
                }
            }

            // 3. Check Rear Fuel Tank
            bool nearFuelTank = IsBike
                ? (local.z > -0.45f && local.z < 0.45f && local.y > 0.45f)
                : (local.z < -0.90f && Mathf.Abs(local.x) > 0.50f && local.y > 0.22f && local.y < 0.92f);

            if (nearFuelTank && !FuelBurning)
            {
                fuelTankDamage += damage;
                effects?.PlayAt(CostaEffects.Cue.MetalHit, worldHitPoint, 0.5f);
                if (fuelTankDamage >= 35)
                {
                    FuelBurning = true;
                    FuelBurnTimer = 2.5f;
                    EnsureFuelFlame(true);
                    effects?.PlayAt(CostaEffects.Cue.Alert, worldHitPoint, 0.6f);
                    partHit = L("Fuel tank leaking fire! Get clear!", "Tanque em chamas! Afaste-se!");
                    return true;
                }
            }

            return false;
        }

        internal bool TickCombat(float dt, CostaEffects effects, out bool explodedNow)
        {
            explodedNow = false;
            if (!FuelBurning)
                return false;

            FuelBurnTimer -= dt;
            if (fuelFlame != null)
            {
                float pulse = 0.85f + Mathf.Sin(Time.time * 28f) * 0.25f;
                fuelFlame.localScale = new Vector3(0.42f * pulse, 0.62f * pulse, 0.42f * pulse);
            }

            if (FuelBurnTimer <= 0f)
            {
                FuelBurning = false;
                Wrecked = true;
                speed = 0f;
                slip = 0f;
                EnsureFuelFlame(false);
                SetDamage(Vector2.one);
                effects?.Explosion(Root.position + Vector3.up * 0.85f);
                explodedNow = true;
                return true;
            }
            return false;
        }

        internal void ApplyCombatState(int tireMask, int glassMask, bool fuelBurning, bool wrecked, CostaEffects effects)
        {
            if (tireMask != TireMask)
            {
                TireMask = tireMask;
                for (int i = 0; i < wheels.Length; i++)
                    ApplyWheelVisual(i, (TireMask & (1 << i)) != 0);
            }
            if (glassMask != GlassMask)
            {
                bool brokeMore = (glassMask & ~GlassMask) != 0;
                GlassMask = glassMask;
                ApplyGlassVisual();
                if (brokeMore)
                    effects?.PlayAt(CostaEffects.Cue.Glass, Root.position + Vector3.up * 1.1f, 0.5f);
            }
            if (fuelBurning != FuelBurning)
            {
                FuelBurning = fuelBurning;
                if (fuelBurning) FuelBurnTimer = 2.5f;
                EnsureFuelFlame(fuelBurning);
            }
            if (wrecked && !Wrecked)
            {
                Wrecked = true;
                speed = 0f;
                EnsureFuelFlame(false);
                SetDamage(Vector2.one);
                effects?.Explosion(Root.position + Vector3.up * 0.85f);
            }
            else if (!wrecked && Wrecked)
            {
                Wrecked = false;
            }
        }

        internal void ResetCombat()
        {
            TireMask = 0;
            GlassMask = 0;
            FuelBurning = false;
            FuelBurnTimer = 0f;
            Wrecked = false;
            fuelTankDamage = 0;
            EnsureFuelFlame(false);
            for (int i = 0; i < wheels.Length; i++)
                ApplyWheelVisual(i, false);
            ApplyGlassVisual();
        }

        private void ApplyWheelVisual(int index, bool burst)
        {
            if (index < 0 || index >= wheels.Length || wheels[index] == null)
                return;
            wheels[index].localScale = burst ? new Vector3(0.86f, 0.76f, 0.86f) : Vector3.one;
        }

        private void ApplyGlassVisual()
        {
            if (glassRenderers == null)
                return;
            bool shattered = (GlassMask & 1) != 0 || CountBits(GlassMask) >= 2;
            for (int i = 0; i < glassRenderers.Length; i++)
            {
                if (glassRenderers[i] != null)
                    glassRenderers[i].enabled = !shattered;
            }
        }

        private void EnsureFuelFlame(bool active)
        {
            if (!active)
            {
                if (fuelFlame != null)
                    fuelFlame.gameObject.SetActive(false);
                return;
            }
            if (fuelFlame == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "FuelLeakFlame";
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(shell, false);
                go.transform.localPosition = IsBike ? new Vector3(0f, 0.65f, 0f) : new Vector3(-0.88f, 0.56f, -1.55f);
                Renderer r = go.GetComponent<Renderer>();
                r.material.color = new Color(1f, 0.42f, 0.08f);
                fuelFlame = go.transform;
            }
            fuelFlame.gameObject.SetActive(true);
        }

        private static int CountBits(int mask)
        {
            int c = 0;
            while (mask != 0) { mask &= mask - 1; c++; }
            return c;
        }
    }
}
