using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using static CostaLivre.CostaText;
using Floor = CostaLivre.CostaAcousticScene.Floor;

namespace CostaLivre
{
    internal sealed class CostaStealthHeist
    {
        private sealed class LootItem
        {
            internal GameObject Visual;
            internal Vector3 Position;
            internal int Value;
            internal bool IsSafe;
            internal bool Collected;
            internal string NameEn, NamePt;
        }

        private sealed class Resident
        {
            internal HarborCharacter Body;
            internal Vector3 Home;
            internal Vector3 PatrolA, PatrolB;
            internal Vector3 CurrentPos;
            internal float Yaw;
            internal bool Sleeping;
            internal bool IsGuard;
            internal bool PatrollingToB;
            internal float Suspicion; // 0..1
            internal bool Alerted;
        }

        private sealed class House
        {
            internal string NameEn, NamePt;
            internal Vector3 Center;
            internal Vector3 DoorPos;
            internal Transform DoorHinge;
            internal Collider DoorCollider;
            internal Bounds InteriorBounds;
            internal int LinkedEstateIndex = -1; // >= 0 for Vila 01..03
            internal bool Unlocked;
            internal float DoorAngle;
            internal bool AlertTriggered;
            internal bool StealthBonusPaid;
            internal bool LootedSafe;
            internal float ResetAt;
            internal readonly List<LootItem> Loot = new();
            internal readonly List<Resident> Residents = new();
        }

        internal static readonly Vector3[] NewHouseCenters =
        {
            new(-76f, 0f, 138f),
            new(4f, 0f, 138f),
            new(-84f, 0f, CostaCity.Districts[0].Center + 38f)
        };

        private const float ActiveRadius = 28f;
        private const float VisionRange = 11f;
        private const float VisionCosHalfAngle = 0.82f; // ~65 deg full cone

        private readonly List<House> houses = new();
        private readonly HarborBlockout art;
        private int activeHouse = -1;

        // Acoustic state
        private Floor currentFloor = Floor.Stone;
        private float currentNoise;      // 0..1
        private float noiseRadius;       // 2..18m
        private float noiseImpulse;      // decays over time
        private string postureLabel = "";

        // Lockpick minigame state
        private bool lockpicking;
        private int lockpickHouse = -1;
        private LootItem lockpickSafe;
        private int lockpickPinsLeft;
        private float lockpickCursor;
        private float lockpickTarget;
        private float lockpickDir = 1f;

        private GUIStyle hudStyle;
        private GUIStyle smallStyle;
        private GUIStyle tagStyle;

        internal bool BlocksMovement => lockpicking;
        internal string Prompt { get; private set; } = "";

        internal CostaStealthHeist(HarborBlockout art, Transform parent)
        {
            this.art = art;
            Transform root = new GameObject("StealthResidences").transform;
            root.SetParent(parent, false);

            // 3 new furnished residential houses
            BuildNewHouse(root, "Mirante Residence", "Casa Mirante", NewHouseCenters[0],
                new Color(0.90f, 0.84f, 0.74f), new Color(0.74f, 0.36f, 0.28f), 2600);
            BuildNewHouse(root, "Coral Residence", "Residência Coral", NewHouseCenters[1],
                new Color(0.86f, 0.64f, 0.56f), new Color(0.18f, 0.52f, 0.54f), 2900);
            BuildNewHouse(root, "Calado Mansion", "Mansão Calado", NewHouseCenters[2],
                new Color(0.88f, 0.82f, 0.68f), new Color(0.24f, 0.42f, 0.64f), 3400);

            // 3 existing unowned Vilas (Vila 01..03)
            for (int i = 0; i < CostaCity.Estate.Length; i++)
                RegisterExistingEstate(root, i, CostaCity.Estate[i]);

            // Apartamentos Mirante upper floor (Level 1)
            RegisterApartmentHeist(root);
        }

        private void BuildNewHouse(Transform root, string en, string pt, Vector3 center, Color wall, Color trim, int safeValue)
        {
            float w = 10.4f, d = 9.2f, h = 3.5f;
            float left = center.x - w * 0.5f;
            float right = center.x + w * 0.5f;
            float front = center.z - d * 0.5f;
            float back = center.z + d * 0.5f;

            // Mixed acoustic floor surfaces
            art.MarkFootFloor(art.Box("House stone foyer", new Vector3(center.x, 0.03f, front + 1.5f), new Vector3(w, 0.06f, 3.0f),
                new Color(0.76f, 0.74f, 0.70f), true), Floor.Stone);
            art.MarkFootFloor(art.Box("House wood floor", new Vector3(center.x, 0.03f, center.z + 1.5f), new Vector3(w, 0.06f, 6.2f),
                new Color(0.50f, 0.34f, 0.22f), true), Floor.Wood);
            art.MarkFootFloor(art.Box("Living room carpet", new Vector3(left + 2.6f, 0.068f, center.z - 0.6f), new Vector3(3.4f, 0.016f, 2.8f),
                new Color(0.52f, 0.28f, 0.26f)), Floor.Carpet);
            art.MarkFootFloor(art.Box("Bedroom carpet", new Vector3(right - 2.4f, 0.068f, back - 2.2f), new Vector3(3.6f, 0.016f, 3.0f),
                new Color(0.24f, 0.38f, 0.44f)), Floor.Carpet);
            art.MarkFootFloor(art.Box("Utility metal plate", new Vector3(right - 1.4f, 0.068f, center.z + 0.2f), new Vector3(1.8f, 0.016f, 1.5f),
                new Color(0.46f, 0.49f, 0.52f)), Floor.Metal);

            // Outer walls (front wall has 1.6m doorway in the center)
            art.Box("House front left", new Vector3(center.x - 3.0f, h * 0.5f, front), new Vector3(4.4f, h, 0.26f), wall, true);
            art.Box("House front right", new Vector3(center.x + 3.0f, h * 0.5f, front), new Vector3(4.4f, h, 0.26f), wall, true);
            art.Box("House front lintel", new Vector3(center.x, h - 0.45f, front), new Vector3(1.6f, 0.9f, 0.26f), wall, true);
            art.Box("House back wall", new Vector3(center.x, h * 0.5f, back), new Vector3(w, h, 0.26f), wall, true);
            art.Box("House left wall", new Vector3(left, h * 0.5f, center.z), new Vector3(0.26f, h, d), wall, true);
            art.Box("House right wall", new Vector3(right, h * 0.5f, center.z), new Vector3(0.26f, h, d), wall, true);
            art.Box("House roof", new Vector3(center.x, h + 0.14f, center.z), new Vector3(w + 0.6f, 0.28f, d + 0.6f), trim, true);
            art.Sign(pt.ToUpperInvariant(), new Vector3(center.x, h - 0.12f, front - 0.22f), 0.13f, Color.white);

            // Interior partition separating living room & bedroom (with walkway gap)
            art.Box("Interior partition", new Vector3(center.x + 0.4f, h * 0.5f, center.z + 1.1f), new Vector3(0.22f, h, 4.4f),
                new Color(0.84f, 0.80f, 0.74f), true);

            // Cover furniture (blocks Linecast!)
            art.Box("Sofa cover", new Vector3(left + 2.4f, 0.48f, center.z - 0.8f), new Vector3(2.2f, 0.96f, 0.9f),
                new Color(0.28f, 0.42f, 0.40f), true);
            art.Box("Table cover", new Vector3(left + 2.2f, 0.42f, back - 1.8f), new Vector3(1.7f, 0.84f, 1.1f),
                new Color(0.45f, 0.30f, 0.18f), true);
            art.Box("Bedroom bed", new Vector3(right - 1.8f, 0.36f, back - 1.6f), new Vector3(2.0f, 0.72f, 2.2f),
                new Color(0.78f, 0.80f, 0.84f), true);
            art.Box("Dresser cover", new Vector3(center.x + 1.2f, 0.58f, back - 0.6f), new Vector3(1.4f, 1.16f, 0.55f),
                new Color(0.42f, 0.28f, 0.18f), true);

            // Swinging entry door
            Transform hinge = new GameObject("HeistDoorHinge").transform;
            hinge.SetParent(root, false);
            hinge.position = new Vector3(center.x - 0.78f, 0.04f, front);
            GameObject doorMesh = art.Part(hinge, PrimitiveType.Cube, "Heist door", new Vector3(0.78f, 1.28f, 0f),
                new Vector3(1.56f, 2.56f, 0.12f), new Color(0.38f, 0.24f, 0.16f));
            BoxCollider doorCol = doorMesh.GetComponent<BoxCollider>();
            if (doorCol == null) doorCol = doorMesh.AddComponent<BoxCollider>();

            House house = new House
            {
                NameEn = en,
                NamePt = pt,
                Center = center,
                DoorPos = new Vector3(center.x, 0f, front - 0.4f),
                DoorHinge = hinge,
                DoorCollider = doorCol,
                InteriorBounds = new Bounds(center + Vector3.up * 1.7f, new Vector3(w - 0.4f, h, d - 0.4f))
            };

            // Valuables + Bedroom Safe
            AddLoot(house, root, new Vector3(left + 2.2f, 0.92f, back - 1.8f), 750, false, "Luxury Watch", "Relógio de Luxo", new Color(0.95f, 0.78f, 0.24f));
            AddLoot(house, root, new Vector3(left + 1.1f, 0.65f, front + 2.1f), 850, false, "Laptop", "Notebook", new Color(0.24f, 0.54f, 0.70f));
            AddLoot(house, root, new Vector3(center.x + 1.2f, 1.24f, back - 0.6f), 680, false, "Jewelry Box", "Porta-Joias", new Color(0.84f, 0.22f, 0.34f));
            AddLoot(house, root, new Vector3(right - 0.85f, 0.42f, center.z + 0.6f), safeValue, true, "Bedroom Safe", "Cofre do Quarto", new Color(0.22f, 0.26f, 0.30f));

            // 1 Patrolling resident/guard in living room + 1 Sleeping resident in bedroom
            AddResident(house, root, new Vector3(left + 3.4f, 0f, center.z + 1.6f),
                new Vector3(left + 3.4f, 0f, center.z + 1.6f), new Vector3(left + 3.4f, 0f, front + 2.2f), false, true);
            AddResident(house, root, new Vector3(right - 2.6f, 0f, back - 2.4f),
                new Vector3(right - 2.6f, 0f, back - 2.4f), new Vector3(right - 2.6f, 0f, back - 2.4f), true, false);

            houses.Add(house);
        }

        private void RegisterExistingEstate(Transform root, int estateIdx, Vector3 estatePos)
        {
            House house = new House
            {
                NameEn = $"Vila {estateIdx + 1:00} (Heist)",
                NamePt = $"Vila {estateIdx + 1:00} (Invasão)",
                Center = estatePos + new Vector3(0f, 0f, 3.2f),
                DoorPos = estatePos + new Vector3(0f, 0f, -0.5f),
                LinkedEstateIndex = estateIdx,
                Unlocked = false,
                InteriorBounds = new Bounds(estatePos + new Vector3(0f, 1.6f, 3.2f), new Vector3(8.5f, 3.4f, 7.6f))
            };

            AddLoot(house, root, estatePos + new Vector3(-2.1f, 0.72f, 2.8f), 720, false, "Silverware", "Prataria", new Color(0.76f, 0.80f, 0.84f));
            AddLoot(house, root, estatePos + new Vector3(2.2f, 0.42f, 4.8f), 2200 + estateIdx * 450, true, "Villa Safe", "Cofre da Vila", new Color(0.22f, 0.25f, 0.28f));
            AddResident(house, root, estatePos + new Vector3(-1.4f, 0f, 4.6f),
                estatePos + new Vector3(-1.4f, 0f, 4.6f), estatePos + new Vector3(1.4f, 0f, 2.2f), true, false);

            houses.Add(house);
        }

        private void RegisterApartmentHeist(Transform root)
        {
            CostaInteriorPlan apt = CostaInteriorPlan.Apartment;
            float y = apt.Y(1);
            Vector3 center = new Vector3(apt.Entry.x, y, apt.Front + apt.Depth * 0.5f);
            House house = new House
            {
                NameEn = "Mirante Apartments (2F)",
                NamePt = "Apartamentos Mirante (2º Andar)",
                Center = center,
                DoorPos = new Vector3(apt.Entry.x, y, apt.Front + 2.2f),
                Unlocked = true, // accessible via elevator/stairs
                InteriorBounds = new Bounds(center + Vector3.up * 1.5f, new Vector3(apt.Width - 0.6f, 3.1f, apt.Depth - 0.6f))
            };

            AddLoot(house, root, new Vector3(apt.Left + 2.6f, y + 0.75f, apt.Back - 3.0f), 920, false, "Designer Watch", "Relógio Importado", new Color(0.95f, 0.76f, 0.24f));
            AddLoot(house, root, new Vector3(apt.Right - 2.6f, y + 0.42f, apt.Back - 2.6f), 2800, true, "Penthouse Safe", "Cofre do Apartamento", new Color(0.20f, 0.24f, 0.28f));
            AddResident(house, root, new Vector3(apt.Entry.x - 1.2f, y, apt.Front + 5.0f),
                new Vector3(apt.Entry.x - 1.2f, y, apt.Front + 5.0f), new Vector3(apt.Entry.x + 1.4f, y, apt.Back - 4.2f), false, true);

            houses.Add(house);
        }

        private void AddLoot(House house, Transform root, Vector3 pos, int value, bool isSafe, string en, string pt, Color color)
        {
            GameObject go = art.Part(root, PrimitiveType.Cube, isSafe ? "StealthSafe" : "StealthValuable",
                pos, isSafe ? new Vector3(0.62f, 0.72f, 0.56f) : new Vector3(0.32f, 0.16f, 0.24f), color);
            house.Loot.Add(new LootItem
            {
                Visual = go,
                Position = pos,
                Value = value,
                IsSafe = isSafe,
                NameEn = en,
                NamePt = pt
            });
        }

        private void AddResident(House house, Transform root, Vector3 home, Vector3 a, Vector3 b, bool sleeping, bool guard)
        {
            HarborCharacter body = art.Person(guard ? "HouseGuard" : "HouseResident", root, home,
                guard ? new Color(0.22f, 0.26f, 0.34f) : Color.white);
            Texture2D look = Resources.Load<Texture2D>($"Harbor/Wardrobe/look-{(houses.Count * 2 + house.Residents.Count) % 10 + 1}");
            if (look != null) body.WearTexture(look);
            body.Root.gameObject.SetActive(false);

            house.Residents.Add(new Resident
            {
                Body = body,
                Home = home,
                PatrolA = a,
                PatrolB = b,
                CurrentPos = home,
                Sleeping = sleeping,
                IsGuard = guard
            });
        }

        internal bool Update(
            Vector3 playerPos,
            float horizontalSpeed,
            bool crouching,
            bool running,
            bool interactPressed,
            bool kickPressed,
            bool shotFired,
            CostaJobs jobs,
            CostaEffects effects,
            float delta,
            Action<string> onNotice,
            Action onPoliceAlert)
        {
            Prompt = "";

            // Find nearest active target residence
            int nearest = -1;
            float bestDistSq = ActiveRadius * ActiveRadius;
            for (int i = 0; i < houses.Count; i++)
            {
                House h = houses[i];
                if (h.LinkedEstateIndex >= 0 && jobs.Owned(h.LinkedEstateIndex))
                    continue;
                if (h.ResetAt > 0f && Time.time >= h.ResetAt && (h.Center - playerPos).sqrMagnitude > 22f * 22f)
                    ResetHouse(h);

                float dSq = (h.Center - playerPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    nearest = i;
                }
            }

            if (activeHouse != nearest)
            {
                if (activeHouse >= 0) SetResidentsVisible(houses[activeHouse], false);
                activeHouse = nearest;
                if (activeHouse >= 0) SetResidentsVisible(houses[activeHouse], true);
            }

            if (activeHouse < 0)
            {
                lockpicking = false;
                return false;
            }

            House active = houses[activeHouse];
            bool inside = active.InteriorBounds.Contains(playerPos + Vector3.up * 0.8f);

            // Animate door swing
            if (active.DoorHinge != null)
            {
                float targetAngle = active.Unlocked ? 92f : 0f;
                active.DoorAngle = Mathf.MoveTowards(active.DoorAngle, targetAngle, delta * 260f);
                active.DoorHinge.localRotation = Quaternion.Euler(0f, active.DoorAngle, 0f);
                if (active.DoorCollider != null)
                    active.DoorCollider.enabled = !active.Unlocked;
            }

            // Compute surface-based acoustic noise
            currentFloor = art.Acoustics.FloorAt(playerPos, art.Damage);
            float surfaceMult = currentFloor switch
            {
                Floor.Carpet => 0.35f,
                Floor.Earth => 0.75f,
                Floor.Stone or Floor.Asphalt => 1.00f,
                Floor.Wood => 1.15f,
                Floor.Metal => 1.45f,
                _ => 1.00f
            };
            float speedFactor;
            if (horizontalSpeed < 0.15f)
            {
                speedFactor = 0.04f;
                postureLabel = L("Still", "Parado");
            }
            else if (crouching)
            {
                speedFactor = 0.35f;
                postureLabel = L("Crouched", "Agachado");
            }
            else if (running)
            {
                speedFactor = 2.10f;
                postureLabel = L("Running", "Correndo");
            }
            else
            {
                speedFactor = 1.00f;
                postureLabel = L("Walking", "Andando");
            }

            if (shotFired) noiseImpulse = 1f;
            noiseImpulse = Mathf.Max(0f, noiseImpulse - delta * 0.65f);
            float stepNoise = Mathf.Clamp01(speedFactor * surfaceMult * 0.42f);
            currentNoise = Mathf.Clamp01(Mathf.Max(stepNoise, noiseImpulse));
            noiseRadius = Mathf.Lerp(2f, 18f, currentNoise);

            // Handle active lockpick minigame
            if (lockpicking)
            {
                lockpickCursor += lockpickDir * delta * 1.35f;
                if (lockpickCursor >= 1f) { lockpickCursor = 1f; lockpickDir = -1f; }
                else if (lockpickCursor <= 0f) { lockpickCursor = 0f; lockpickDir = 1f; }

                bool attempt = interactPressed || Mouse.current?.leftButton.wasPressedThisFrame == true;
                bool cancel = Keyboard.current?.escapeKey.wasPressedThisFrame == true;
                if (cancel)
                {
                    lockpicking = false;
                    return true;
                }
                if (attempt)
                {
                    if (Mathf.Abs(lockpickCursor - lockpickTarget) <= 0.14f)
                    {
                        lockpickPinsLeft--;
                        effects.Play(CostaEffects.Cue.Equip, 0.35f);
                        if (lockpickPinsLeft <= 0)
                        {
                            lockpicking = false;
                            if (lockpickSafe != null)
                            {
                                CollectLoot(active, lockpickSafe, jobs, effects, onNotice);
                                lockpickSafe = null;
                            }
                            else if (lockpickHouse >= 0)
                            {
                                houses[lockpickHouse].Unlocked = true;
                                effects.Play(CostaEffects.Cue.PanelOpen, 0.3f);
                                onNotice?.Invoke(L("Door unlocked silently!", "Porta destrancada silenciosamente!"));
                            }
                        }
                        else
                        {
                            lockpickTarget = UnityEngine.Random.Range(0.22f, 0.78f);
                        }
                    }
                    else
                    {
                        noiseImpulse = Mathf.Max(noiseImpulse, 0.68f);
                        effects.Play(CostaEffects.Cue.MetalHit, 0.38f);
                        onNotice?.Invoke(L("Lockpick slipped! Noise generated.", "Gazua escapou! Ruído gerado."));
                    }
                }
                return true;
            }

            // Stealth escape bonus check
            if (!inside && active.LootedSafe && !active.AlertTriggered && !active.StealthBonusPaid)
            {
                active.StealthBonusPaid = true;
                jobs.Ledger.SaveSolo(jobs.Account, 1000);
                effects.Play(CostaEffects.Cue.Sale, 0.55f);
                onNotice?.Invoke(L("Ghost Escape! +$1,000 stealth bonus", "Fuga Fantasma! +$1.000 bônus furtivo"));
            }

            // Door breach prompt & actions (for locked houses)
            bool consumedInteract = false;
            if (!active.Unlocked && (playerPos - active.DoorPos).sqrMagnitude <= 2.35f * 2.35f)
            {
                Prompt = L("[E] Silent Lockpick  ·  [R] Kick Door (Loud)",
                    "[E] Arrombar Silencioso (Gazua)  ·  [R] Chutar Porta (Barulhento)");
                if (interactPressed)
                {
                    StartLockpick(activeHouse, null, 2);
                    consumedInteract = true;
                }
                else if (kickPressed)
                {
                    active.Unlocked = true;
                    noiseImpulse = 1.0f; // 2.0x max burst
                    effects.Play(CostaEffects.Cue.Blast, 0.52f);
                    onNotice?.Invoke(L("Door kicked open! Residents heard you!", "Porta arrombada no chute! Moradores ouviram!"));
                    consumedInteract = true;
                }
            }

            // Loot & Safe interaction inside house
            if (!consumedInteract && (active.Unlocked || inside))
            {
                for (int i = 0; i < active.Loot.Count; i++)
                {
                    LootItem item = active.Loot[i];
                    if (item.Collected) continue;
                    if ((playerPos - item.Position).sqrMagnitude <= 2.0f * 2.0f)
                    {
                        Prompt = item.IsSafe
                            ? L($"[E] Crack {item.NameEn} (${item.Value})", $"[E] Abrir {item.NamePt} (${item.Value})")
                            : L($"[E] Steal {item.NameEn} (${item.Value})", $"[E] Pegar {item.NamePt} (${item.Value})");
                        if (interactPressed)
                        {
                            if (item.IsSafe)
                                StartLockpick(activeHouse, item, 3);
                            else
                                CollectLoot(active, item, jobs, effects, onNotice);
                            consumedInteract = true;
                        }
                        break;
                    }
                }
            }

            // Update residents & guards (vision cone + acoustic detection)
            Vector3 playerChest = playerPos + Vector3.up * (crouching ? 0.72f : 1.25f);
            for (int r = 0; r < active.Residents.Count; r++)
            {
                Resident res = active.Residents[r];
                float distToPlayer = Vector3.Distance(res.CurrentPos, playerPos);

                // Acoustic check
                bool heardPlayer = distToPlayer <= noiseRadius && (currentNoise > (res.Sleeping ? 0.48f : 0.28f));
                if (res.Sleeping && heardPlayer)
                {
                    res.Sleeping = false;
                    res.Suspicion = Mathf.Max(res.Suspicion, 0.35f);
                    onNotice?.Invoke(L("A resident woke up from the noise!", "Um morador acordou com o barulho!"));
                }

                // Patrol movement when awake & guard
                bool moving = false;
                if (!res.Sleeping && !res.Alerted && (res.PatrolA - res.PatrolB).sqrMagnitude > 0.25f)
                {
                    Vector3 target = res.PatrollingToB ? res.PatrolB : res.PatrolA;
                    Vector3 toTarget = target - res.CurrentPos;
                    toTarget.y = 0f;
                    if (toTarget.magnitude < 0.25f)
                    {
                        res.PatrollingToB = !res.PatrollingToB;
                    }
                    else
                    {
                        Vector3 dir = toTarget.normalized;
                        res.CurrentPos += dir * (1.35f * delta);
                        res.Yaw = Mathf.LerpAngle(res.Yaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, delta * 5f);
                        moving = true;
                    }
                }
                else if (heardPlayer)
                {
                    Vector3 dir = playerPos - res.CurrentPos;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.05f)
                        res.Yaw = Mathf.LerpAngle(res.Yaw, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, delta * 6f);
                }

                // Vision cone check (~65 deg, 11m, Linecast occlusion by walls/furniture)
                bool seesPlayer = false;
                if (!res.Sleeping && inside && distToPlayer <= VisionRange)
                {
                    Vector3 eye = res.CurrentPos + Vector3.up * 1.52f;
                    Vector3 toPlayer = playerChest - eye;
                    Vector3 forward = Quaternion.Euler(0f, res.Yaw, 0f) * Vector3.forward;
                    if (Vector3.Dot(forward, toPlayer.normalized) >= VisionCosHalfAngle)
                    {
                        if (!Physics.Linecast(eye, playerChest, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                            seesPlayer = true;
                    }
                }

                if (seesPlayer)
                    res.Suspicion = Mathf.Min(1f, res.Suspicion + (0.58f + (crouching ? 0f : 0.28f)) * delta);
                else if (heardPlayer && inside)
                    res.Suspicion = Mathf.Min(1f, res.Suspicion + currentNoise * 0.45f * delta);
                else
                    res.Suspicion = Mathf.Max(0f, res.Suspicion - 0.18f * delta);

                if (res.Suspicion >= 1f && !res.Alerted)
                {
                    res.Alerted = true;
                    if (!active.AlertTriggered)
                    {
                        active.AlertTriggered = true;
                        onNotice?.Invoke(L("Spotted! Police have been called!", "Você foi visto! A polícia foi acionada!"));
                        onPoliceAlert?.Invoke();
                    }
                }

                res.Body.Root.position = res.CurrentPos;
                res.Body.Root.rotation = Quaternion.Euler(0f, res.Yaw, 0f);
                res.Body.AnimateDistance(moving ? HarborCharacter.Motion.Walk : HarborCharacter.Motion.Idle, distToPlayer * distToPlayer);
                if (res.Sleeping)
                    res.Body.CrouchPose();
            }

            return consumedInteract;
        }

        internal void DrawHud(Camera cam)
        {
            if (activeHouse < 0 || cam == null)
                return;
            EnsureStyles();

            House active = houses[activeHouse];

            // 1. Resident overhead suspicion tags
            for (int r = 0; r < active.Residents.Count; r++)
            {
                Resident res = active.Residents[r];
                Vector3 head = res.CurrentPos + Vector3.up * (res.Sleeping ? 1.45f : 2.05f);
                Vector3 screen = cam.WorldToScreenPoint(head);
                if (screen.z <= 0.3f || screen.z > 18f) continue;

                float x = screen.x;
                float y = Screen.height - screen.y;
                float w = 94f;
                string label = res.Alerted ? L("Alert!", "Alerta!")
                    : res.Sleeping ? L("Sleeping", "Dormindo")
                    : res.Suspicion > 0.05f ? $"{Mathf.RoundToInt(res.Suspicion * 100f)}%"
                    : (res.IsGuard ? L("Guard", "Segurança") : L("Resident", "Morador"));

                GUI.color = new Color(0.05f, 0.08f, 0.11f, 0.78f);
                GUI.DrawTexture(new Rect(x - w * 0.5f - 2, y - 18, w + 4, 26), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x - w * 0.5f, y - 17, w, 15), label, tagStyle);
                GUI.color = new Color(0.18f, 0.22f, 0.25f, 0.9f);
                GUI.DrawTexture(new Rect(x - w * 0.5f, y - 1, w, 6f), Texture2D.whiteTexture);
                GUI.color = res.Alerted ? new Color(0.95f, 0.22f, 0.20f)
                    : res.Suspicion > 0.5f ? new Color(0.96f, 0.56f, 0.18f)
                    : new Color(0.92f, 0.80f, 0.26f);
                GUI.DrawTexture(new Rect(x - w * 0.5f, y - 1, w * Mathf.Clamp01(res.Suspicion), 6f), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }

            // 2. Noise Meter (bottom-left compact stealth bar)
            float nw = 218f;
            float nx = 22f;
            float ny = Screen.height - 142f;
            GUI.color = new Color(0.05f, 0.08f, 0.11f, 0.82f);
            GUI.DrawTexture(new Rect(nx - 8, ny - 24, nw + 16, 48), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string floorName = currentFloor switch
            {
                Floor.Carpet => L("Carpet 0.35x", "Carpete 0.35x"),
                Floor.Earth => L("Earth 0.75x", "Terra 0.75x"),
                Floor.Wood => L("Wood 1.15x", "Madeira 1.15x"),
                Floor.Metal => L("Metal 1.45x", "Metal 1.45x"),
                _ => L("Stone 1.0x", "Pedra 1.0x")
            };
            GUI.Label(new Rect(nx, ny - 21, nw, 17), L($"NOISE · {noiseRadius:0.0}m", $"RUÍDO · {noiseRadius:0.0}m"), hudStyle);
            GUI.Label(new Rect(nx, ny + 10, nw, 15), $"{floorName}  ·  {postureLabel}", smallStyle);

            GUI.color = new Color(0.18f, 0.23f, 0.26f, 0.9f);
            GUI.DrawTexture(new Rect(nx, ny - 1, nw, 8), Texture2D.whiteTexture);
            GUI.color = currentNoise < 0.35f ? new Color(0.24f, 0.86f, 0.56f)
                : currentNoise < 0.68f ? new Color(0.95f, 0.74f, 0.24f)
                : new Color(0.96f, 0.26f, 0.22f);
            GUI.DrawTexture(new Rect(nx, ny - 1, nw * currentNoise, 8), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 3. Inline Lockpick Minigame
            if (lockpicking)
            {
                float lw = 280f;
                float lx = (Screen.width - lw) * 0.5f;
                float ly = Screen.height * 0.62f;
                GUI.color = new Color(0.05f, 0.08f, 0.11f, 0.90f);
                GUI.DrawTexture(new Rect(lx - 12, ly - 32, lw + 24, 72), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(lx, ly - 28, lw, 20),
                    L($"LOCKPICK · Pins left: {lockpickPinsLeft}  ([E] / Click)", $"ARROMBAR · Pinos: {lockpickPinsLeft}  ([E] / Clique)"), tagStyle);

                // Track
                GUI.color = new Color(0.18f, 0.22f, 0.26f, 1f);
                GUI.DrawTexture(new Rect(lx, ly, lw, 16), Texture2D.whiteTexture);
                // Green sweet spot
                GUI.color = new Color(0.22f, 0.84f, 0.48f, 0.9f);
                GUI.DrawTexture(new Rect(lx + (lockpickTarget - 0.14f) * lw, ly, lw * 0.28f, 16), Texture2D.whiteTexture);
                // Moving pin cursor
                GUI.color = Color.white;
                GUI.DrawTexture(new Rect(lx + lockpickCursor * lw - 3f, ly - 4f, 6f, 24f), Texture2D.whiteTexture);
            }
        }

        private void StartLockpick(int houseIdx, LootItem safe, int pins)
        {
            lockpicking = true;
            lockpickHouse = houseIdx;
            lockpickSafe = safe;
            lockpickPinsLeft = pins;
            lockpickCursor = 0.1f;
            lockpickDir = 1f;
            lockpickTarget = UnityEngine.Random.Range(0.28f, 0.72f);
        }

        private void CollectLoot(House house, LootItem item, CostaJobs jobs, CostaEffects effects, Action<string> onNotice)
        {
            item.Collected = true;
            if (item.Visual != null) item.Visual.SetActive(false);
            jobs.Ledger.SaveSolo(jobs.Account, item.Value);
            effects.Play(CostaEffects.Cue.Loot, 0.5f);
            if (item.IsSafe)
                house.LootedSafe = true;
            house.ResetAt = Time.time + 150f;
            onNotice?.Invoke(L($"Stolen {item.NameEn}: +${item.Value}", $"Roubado {item.NamePt}: +${item.Value}"));
        }

        private void ResetHouse(House h)
        {
            h.Unlocked = h.DoorHinge == null && h.LinkedEstateIndex < 0;
            h.AlertTriggered = false;
            h.StealthBonusPaid = false;
            h.LootedSafe = false;
            h.ResetAt = 0f;
            for (int i = 0; i < h.Loot.Count; i++)
            {
                h.Loot[i].Collected = false;
                if (h.Loot[i].Visual != null) h.Loot[i].Visual.SetActive(true);
            }
            for (int r = 0; r < h.Residents.Count; r++)
            {
                Resident res = h.Residents[r];
                res.CurrentPos = res.Home;
                res.Suspicion = 0f;
                res.Alerted = false;
                res.Sleeping = !res.IsGuard;
            }
        }

        private static void SetResidentsVisible(House h, bool visible)
        {
            for (int r = 0; r < h.Residents.Count; r++)
                h.Residents[r].Body.Root.gameObject.SetActive(visible);
        }

        private void EnsureStyles()
        {
            if (hudStyle != null) return;
            hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            hudStyle.normal.textColor = new Color(0.95f, 0.96f, 0.94f);
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            smallStyle.normal.textColor = new Color(0.75f, 0.80f, 0.82f);
            tagStyle = new GUIStyle(hudStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
        }
    }
}
