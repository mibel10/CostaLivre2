using System;
using UnityEngine;
using UnityEngine.AI;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaTerritorySystem
    {
        internal const int PlayerFaction = 0;
        internal const int TerritoryCount = 7;

        internal sealed class Zone
        {
            internal readonly int Index;
            internal readonly string En;
            internal readonly string Pt;
            internal readonly Rect Bounds;
            internal readonly int InitialOwner;
            internal readonly int IncomePerCycle;
            internal int Owner;
            internal string Name => L(En, Pt);
            internal Vector3 Center => new(Bounds.center.x, 0f, Bounds.center.y);

            internal Zone(int index, string en, string pt, Rect bounds, int initialOwner, int income)
            {
                Index = index;
                En = en;
                Pt = pt;
                Bounds = bounds;
                InitialOwner = initialOwner;
                Owner = initialOwner;
                IncomePerCycle = income;
            }
        }

        private sealed class Member
        {
            internal HarborCharacter Body;
            internal NavMeshAgent Agent;
            internal CapsuleCollider Hitbox;
            internal Renderer Bandana;
            internal int Health;
            internal int Territory;
            internal int Faction;
            internal bool Active;
            internal bool IsBoss;
            internal float FireCooldown;
            internal float ThinkAt;
        }

        private static readonly Color[] FactionColors =
        {
            new(0.18f, 0.80f, 0.56f, 0.28f), // 0: Costa Livre (Jogador)
            new(0.12f, 0.52f, 0.68f, 0.28f), // 1: Tubarões do Cais
            new(0.88f, 0.58f, 0.18f, 0.28f), // 2: Sindicato Solar
            new(0.86f, 0.26f, 0.50f, 0.28f)  // 3: Coroas de Vice
        };

        private static readonly Color[] FactionSolidColors =
        {
            new(0.20f, 0.84f, 0.58f),
            new(0.14f, 0.56f, 0.72f),
            new(0.92f, 0.62f, 0.20f),
            new(0.90f, 0.30f, 0.54f)
        };

        internal static Color FactionMapColor(int faction) =>
            FactionColors[Mathf.Clamp(faction, 0, FactionColors.Length - 1)];

        internal static Color FactionSolidColor(int faction) =>
            FactionSolidColors[Mathf.Clamp(faction, 0, FactionSolidColors.Length - 1)];

        internal static string FactionName(int faction) => faction switch
        {
            0 => L("Costa Livre (Yours)", "Costa Livre (Seu)"),
            1 => L("Quay Sharks", "Tubarões do Cais"),
            2 => L("Solar Syndicate", "Sindicato Solar"),
            _ => L("Vice Crowns", "Coroas de Vice")
        };

        private readonly Zone[] zones =
        {
            new(0, "Santa Luz Docks",    "Docas de Santa Luz", new Rect(-25, -24, 52, 46),   1, 250),
            new(1, "Historic Center",    "Centro Histórico",   new Rect(-108, 30, 82, 72),   2, 300),
            new(2, "Mirante Waterfront", "Orla do Mirante",    new Rect(-24, 30, 96, 108),   2, 320),
            new(3, "Calado Market",      "Mercado de Calado",  new Rect(-102, 204, 150, 122),1, 350),
            new(4, "Antutu District",    "Distrito Antutu",    new Rect(-102, 434, 150, 122),2, 420),
            new(5, "Vice Boulevard",     "Boulevard Vice",     new Rect(-102, 658, 76, 124), 3, 480),
            new(6, "North Mirante",      "Mirante Norte",      new Rect(-24, 658, 72, 124),  3, 550)
        };

        private readonly Member[] pool = new Member[6];
        private readonly Transform root;
        private readonly MaterialPropertyBlock bandanaProps = new();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        // Active war / defense state
        private int warZone = -1;
        private int warWave;          // 1..3
        private bool warIsDefense;
        private int warKillsToStart;
        private int warTriggerZone = -1;
        private float outsideWarTimer;
        private int defenseZone = -1;
        private float defenseDeadline;
        private float nextIncomeAt;
        private float nextDefenseAt;
        private float nextPatrolCheck;

        internal Zone[] Zones => zones;
        internal int ContestedZone => warZone >= 0 ? warZone : defenseZone;
        internal bool WarActive => warZone >= 0;
        internal int DamageThisFrame { get; private set; }
        internal Vector3? DamageSource { get; private set; }
        internal string Banner { get; private set; } = "";

        internal CostaTerritorySystem(HarborBlockout art, Transform parent, int savedMask)
        {
            root = new GameObject("Gang territories").transform;
            root.SetParent(parent, false);
            ApplyPlayerMask(savedMask);

            for (int i = 0; i < pool.Length; i++)
            {
                HarborCharacter body = art.Person($"Gang member {i + 1}", root, Vector3.zero, Color.white);
                body.WearTexture(Resources.Load<Texture2D>($"Harbor/Wardrobe/look-{(i * 3 + 2) % 24}"));
                GameObject bandanaObj = art.Part(body.Head, PrimitiveType.Cube, "Gang bandana",
                    new Vector3(0, 0.045f, 0.01f), new Vector3(0.225f, 0.045f, 0.23f), Color.white);
                var agent = body.Root.gameObject.AddComponent<NavMeshAgent>();
                agent.radius = 0.38f;
                agent.height = 1.8f;
                agent.speed = 4.2f;
                agent.acceleration = 14f;
                agent.angularSpeed = 420f;
                agent.stoppingDistance = 9f;
                var hitbox = body.Root.gameObject.AddComponent<CapsuleCollider>();
                hitbox.center = new Vector3(0, 0.9f, 0);
                hitbox.radius = 0.42f;
                hitbox.height = 1.8f;
                body.Root.gameObject.SetActive(false);

                pool[i] = new Member
                {
                    Body = body,
                    Agent = agent,
                    Hitbox = hitbox,
                    Bandana = bandanaObj.GetComponent<Renderer>(),
                    Territory = -1
                };
            }

            nextIncomeAt = Time.time + 90f;
            nextDefenseAt = Time.time + 260f;
        }

        internal void ApplyPlayerMask(int mask)
        {
            for (int i = 0; i < zones.Length; i++)
                zones[i].Owner = (mask & (1 << i)) != 0 ? PlayerFaction : zones[i].InitialOwner;
        }

        internal int PlayerMask()
        {
            int mask = 0;
            for (int i = 0; i < zones.Length; i++)
                if (zones[i].Owner == PlayerFaction)
                    mask |= 1 << i;
            return mask;
        }

        internal int OwnedCount()
        {
            int count = 0;
            for (int i = 0; i < zones.Length; i++)
                if (zones[i].Owner == PlayerFaction)
                    count++;
            return count;
        }

        internal int IncomePerCycle()
        {
            int sum = 0;
            for (int i = 0; i < zones.Length; i++)
                if (zones[i].Owner == PlayerFaction)
                    sum += zones[i].IncomePerCycle;
            return sum;
        }

        internal void ApplyNetworkOwners(int[] owners, int contested)
        {
            if (owners == null) return;
            for (int i = 0; i < zones.Length && i < owners.Length; i++)
                zones[i].Owner = Mathf.Clamp(owners[i], 0, 3);
            if (contested >= 0 && contested < zones.Length)
                defenseZone = contested;
            else if (warZone < 0)
                defenseZone = -1;
        }

        internal int ZoneAt(Vector3 position)
        {
            Vector2 p = new(position.x, position.z);
            for (int i = 0; i < zones.Length; i++)
                if (zones[i].Bounds.Contains(p))
                    return i;
            return -1;
        }

        internal void ResetActiveWar()
        {
            warZone = -1;
            warWave = 0;
            warIsDefense = false;
            outsideWarTimer = 0f;
            warKillsToStart = 0;
            warTriggerZone = -1;
            Banner = "";
            DespawnAll();
        }

        internal void Update(Vector3 playerPos, bool rolling, bool online,
            Action<int, int, string> onTerritoryChanged,
            Action<int, string> onPassiveIncome,
            Action<string> onNotice)
        {
            DamageThisFrame = 0;
            DamageSource = null;
            if (online)
            {
                if (warZone >= 0) ResetActiveWar();
                return;
            }

            float now = Time.time;
            float dt = Time.deltaTime;
            int currentZone = ZoneAt(playerPos);

            // 1. Passive income cycle
            if (now >= nextIncomeAt)
            {
                nextIncomeAt = now + 90f;
                int income = IncomePerCycle();
                if (income > 0)
                    onPassiveIncome?.Invoke(income,
                        L($"Territory income: +${income:N0} added to vault", $"Renda dos territórios: +${income:N0} no cofre"));
            }

            // 2. Pending rival counter-attack check
            if (defenseZone >= 0 && warZone < 0)
            {
                if (currentZone == defenseZone)
                {
                    StartWar(defenseZone, defense: true, playerPos, onNotice);
                }
                else if (now >= defenseDeadline)
                {
                    Zone lost = zones[defenseZone];
                    lost.Owner = lost.InitialOwner;
                    defenseZone = -1;
                    onTerritoryChanged?.Invoke(PlayerMask(), 0,
                        L($"{lost.Name} was retaken by {FactionName(lost.Owner)}!",
                          $"{lost.Name} foi retomado por {FactionName(lost.Owner)}!"));
                }
            }
            else if (defenseZone < 0 && warZone < 0 && now >= nextDefenseAt)
            {
                nextDefenseAt = now + UnityEngine.Random.Range(240f, 360f);
                if (OwnedCount() >= 2)
                {
                    int candidate = PickOwnedZone();
                    if (candidate >= 0)
                    {
                        defenseZone = candidate;
                        defenseDeadline = now + 90f;
                        onNotice?.Invoke(L($"ALERT: {zones[candidate].Name} is under attack! Go defend it!",
                            $"ALERTA: {zones[candidate].Name} está sob ataque! Vá defender!"));
                    }
                }
            }

            // 3. Active war management
            if (warZone >= 0)
            {
                if (currentZone != warZone)
                {
                    outsideWarTimer += dt;
                    Banner = L($"Return to {zones[warZone].Name} ({Mathf.CeilToInt(15f - outsideWarTimer)}s)",
                               $"Volte para {zones[warZone].Name} ({Mathf.CeilToInt(15f - outsideWarTimer)}s)");
                    if (outsideWarTimer >= 15f)
                    {
                        string msg = L("Territory war abandoned.", "Guerra de território abandonada.");
                        if (warIsDefense)
                        {
                            zones[warZone].Owner = zones[warZone].InitialOwner;
                            onTerritoryChanged?.Invoke(PlayerMask(), 0,
                                L($"{zones[warZone].Name} lost!", $"{zones[warZone].Name} perdido!"));
                        }
                        else
                            onNotice?.Invoke(msg);
                        ResetActiveWar();
                        return;
                    }
                }
                else
                {
                    outsideWarTimer = 0f;
                    int alive = ActiveMemberCount();
                    if (alive == 0)
                    {
                        int maxWaves = warIsDefense ? 1 : 3;
                        if (warWave < maxWaves)
                        {
                            warWave++;
                            SpawnWave(warZone, warWave, playerPos);
                            onNotice?.Invoke(L($"Wave {warWave}/{maxWaves} incoming!", $"Onda {warWave}/{maxWaves} chegando!"));
                        }
                        else
                        {
                            Zone won = zones[warZone];
                            bool wasDefense = warIsDefense;
                            won.Owner = PlayerFaction;
                            defenseZone = -1;
                            int bonus = wasDefense ? 650 : 900 + won.Index * 125;
                            ResetActiveWar();
                            onTerritoryChanged?.Invoke(PlayerMask(), bonus,
                                wasDefense
                                    ? L($"{won.Name} defended! (+${bonus:N0})", $"{won.Name} defendido! (+${bonus:N0})")
                                    : L($"{won.Name} conquered! (+${bonus:N0} · +${won.IncomePerCycle}/cycle)",
                                        $"{won.Name} dominado! (+${bonus:N0} · +${won.IncomePerCycle}/ciclo)"));
                            return;
                        }
                    }
                    int totalWaves = warIsDefense ? 1 : 3;
                    Banner = L($"GANG WAR · {zones[warZone].Name} · Wave {warWave}/{totalWaves} · Enemies: {alive}",
                               $"GUERRA DE BAIRRO · {zones[warZone].Name} · Onda {warWave}/{totalWaves} · Rivais: {alive}");
                }
            }
            else
            {
                Banner = defenseZone >= 0
                    ? L($"DEFEND {zones[defenseZone].Name} ({Mathf.CeilToInt(Mathf.Max(0, defenseDeadline - now))}s)",
                        $"DEFENDA {zones[defenseZone].Name} ({Mathf.CeilToInt(Mathf.Max(0, defenseDeadline - now))}s)")
                    : "";

                // Spawn light patrol in rival zone
                if (now >= nextPatrolCheck)
                {
                    nextPatrolCheck = now + 1.25f;
                    if (currentZone >= 0 && zones[currentZone].Owner != PlayerFaction)
                    {
                        if (ActiveMemberCount() < 2)
                            SpawnPatrol(currentZone, playerPos);
                    }
                    else if (ActiveMemberCount() > 0)
                    {
                        DespawnAll();
                    }
                }
            }

            // 4. Update active gang members (CPU-friendly throttled AI + AnimateDistance)
            for (int i = 0; i < pool.Length; i++)
            {
                Member m = pool[i];
                if (!m.Active) continue;
                Vector3 pos = m.Body.Root.position;
                Vector3 toPlayer = playerPos - pos;
                float distSq = toPlayer.sqrMagnitude;
                if (distSq > 95f * 95f && warZone < 0)
                {
                    Deactivate(m);
                    continue;
                }

                bool hostile = warZone >= 0 || m.Health < (m.IsBoss ? 140 : 65);
                if (now >= m.ThinkAt && m.Agent.isOnNavMesh)
                {
                    m.ThinkAt = now + 0.35f;
                    if (hostile)
                    {
                        m.Agent.speed = m.IsBoss ? 4.8f : 4.3f;
                        m.Agent.SetDestination(playerPos);
                    }
                    else if (!m.Agent.hasPath || m.Agent.remainingDistance < 1.2f)
                    {
                        m.Agent.speed = 1.9f;
                        Vector3 roam = zones[m.Territory].Center + new Vector3(
                            UnityEngine.Random.Range(-18f, 18f), 0f, UnityEngine.Random.Range(-18f, 18f));
                        if (NavMesh.SamplePosition(roam, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
                            m.Agent.SetDestination(navHit.position);
                    }
                }

                if (hostile && distSq < 28f * 28f)
                {
                    Vector3 flat = new(toPlayer.x, 0f, toPlayer.z);
                    if (flat.sqrMagnitude > 0.1f)
                        m.Body.Root.rotation = Quaternion.RotateTowards(m.Body.Root.rotation,
                            Quaternion.LookRotation(flat), dt * 360f);

                    m.FireCooldown -= dt;
                    if (m.FireCooldown <= 0f)
                    {
                        m.FireCooldown = m.IsBoss ? 0.65f : UnityEngine.Random.Range(0.85f, 1.35f);
                        if (!rolling && !Physics.Linecast(pos + Vector3.up * 1.4f, playerPos + Vector3.up * 1.1f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        {
                            DamageThisFrame += m.IsBoss ? 14 : 9;
                            DamageSource = pos + Vector3.up * 1.3f;
                        }
                    }
                }

                float speedSq = m.Agent.velocity.sqrMagnitude;
                HarborCharacter.Motion motion = speedSq > 9f ? HarborCharacter.Motion.Run
                    : speedSq > 0.2f ? HarborCharacter.Motion.Walk : HarborCharacter.Motion.Idle;
                m.Body.AnimateDistance(motion, distSq, motion == HarborCharacter.Motion.Run ? 1.0f : 0.72f);
            }
        }

        internal bool TryDamageCollider(Collider hit, int damage, Vector3 playerPos, Action<string> onNotice)
        {
            if (hit == null) return false;
            for (int i = 0; i < pool.Length; i++)
            {
                Member m = pool[i];
                if (!m.Active || m.Hitbox != hit) continue;
                m.Health -= damage;
                if (m.Health <= 0)
                {
                    int zone = m.Territory;
                    Deactivate(m);
                    if (warZone < 0 && zone >= 0 && zones[zone].Owner != PlayerFaction)
                    {
                        if (warTriggerZone != zone)
                        {
                            warTriggerZone = zone;
                            warKillsToStart = 0;
                        }
                        warKillsToStart++;
                        if (warKillsToStart >= 2)
                            StartWar(zone, defense: false, playerPos, onNotice);
                        else
                            onNotice?.Invoke(L("Eliminate 1 more rival to start a Gang War!",
                                "Elimine mais 1 rival para iniciar uma Guerra de Bairro!"));
                    }
                }
                return true;
            }
            return false;
        }

        private void StartWar(int zoneIndex, bool defense, Vector3 playerPos, Action<string> onNotice)
        {
            warZone = zoneIndex;
            warWave = 1;
            warIsDefense = defense;
            warKillsToStart = 0;
            outsideWarTimer = 0f;
            DespawnAll();
            SpawnWave(zoneIndex, 1, playerPos);
            onNotice?.Invoke(defense
                ? L($"Defending {zones[zoneIndex].Name} — eliminate the attackers!",
                    $"Defendendo {zones[zoneIndex].Name} — elimine os invasores!")
                : L($"GANG WAR STARTED in {zones[zoneIndex].Name}! Survive 3 waves!",
                    $"GUERRA DE BAIRRO em {zones[zoneIndex].Name}! Vença 3 ondas!"));
        }

        private void SpawnPatrol(int zoneIndex, Vector3 playerPos)
        {
            int faction = zones[zoneIndex].Owner;
            int needed = 2 - ActiveMemberCount();
            for (int n = 0; n < needed; n++)
                SpawnMember(zoneIndex, faction, playerPos, 18f, 34f, boss: false);
        }

        private void SpawnWave(int zoneIndex, int wave, Vector3 playerPos)
        {
            int faction = zones[zoneIndex].Owner == PlayerFaction ? zones[zoneIndex].InitialOwner : zones[zoneIndex].Owner;
            int count = Mathf.Min(pool.Length, wave == 1 ? 3 : wave == 2 ? 4 : 5);
            for (int i = 0; i < count; i++)
                SpawnMember(zoneIndex, faction, playerPos, 16f, 30f, boss: wave == 3 && i == 0);
        }

        private void SpawnMember(int zoneIndex, int faction, Vector3 around, float minR, float maxR, bool boss)
        {
            Member slot = null;
            for (int i = 0; i < pool.Length; i++)
                if (!pool[i].Active) { slot = pool[i]; break; }
            if (slot == null) return;

            Zone zone = zones[zoneIndex];
            Vector3 spawn = zone.Center;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float a = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float r = UnityEngine.Random.Range(minR, maxR);
                Vector3 candidate = around + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                candidate.x = Mathf.Clamp(candidate.x, zone.Bounds.xMin + 3f, zone.Bounds.xMax - 3f);
                candidate.z = Mathf.Clamp(candidate.z, zone.Bounds.yMin + 3f, zone.Bounds.yMax - 3f);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                {
                    spawn = hit.position;
                    break;
                }
            }

            slot.Territory = zoneIndex;
            slot.Faction = faction;
            slot.IsBoss = boss;
            slot.Health = boss ? 140 : 65;
            slot.FireCooldown = UnityEngine.Random.Range(0.7f, 1.4f);
            slot.ThinkAt = 0f;
            slot.Active = true;
            slot.Body.Root.position = spawn;
            slot.Body.Root.localScale = boss ? Vector3.one * 1.10f : Vector3.one;
            slot.Body.Root.gameObject.SetActive(true);
            slot.Agent.Warp(spawn);

            bandanaProps.SetColor(BaseColor, FactionSolidColor(faction));
            slot.Bandana.SetPropertyBlock(bandanaProps);
        }

        private int PickOwnedZone()
        {
            int chosen = -1;
            int seen = 0;
            for (int i = 0; i < zones.Length; i++)
                if (zones[i].Owner == PlayerFaction && UnityEngine.Random.Range(0, ++seen) == 0)
                    chosen = i;
            return chosen;
        }

        private int ActiveMemberCount()
        {
            int count = 0;
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].Active) count++;
            return count;
        }

        private static void Deactivate(Member m)
        {
            m.Active = false;
            m.Territory = -1;
            m.Body.Root.gameObject.SetActive(false);
        }

        private void DespawnAll()
        {
            for (int i = 0; i < pool.Length; i++)
                if (pool[i].Active) Deactivate(pool[i]);
        }

        internal void Release()
        {
            for (int i = 0; i < pool.Length; i++)
                pool[i]?.Body?.Release();
        }
    }
}
