using UnityEngine;
using UnityEngine.AI;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    /// <summary>
    /// Tactical 5-star police pursuit, armed combat, roadblocks, search helicopter, and line-of-sight escape system.
    /// </summary>
    internal sealed class CostaPoliceSystem
    {
        internal sealed class Officer
        {
            internal HarborCharacter Character;
            internal NavMeshAgent Agent;
            internal CapsuleCollider Hitbox;
            internal GameObject Flash;
            internal Vector3 Post;
            internal int Health;
            internal int MaxHealth;
            internal int Ammo;
            internal int MagSize;
            internal float FireCooldown;
            internal float ReloadUntil;
            internal float FlashUntil;
            internal float StrafeDir = 1f;
            internal bool Elite;
        }

        private sealed class PatrolCar
        {
            internal Transform Root;
            internal GameObject[] Beacons;
            internal float Speed;
            internal bool Active;
            internal float DeployCooldown;
        }

        private readonly HarborBlockout art;
        private readonly CostaEffects effects;
        private readonly Transform root;
        private readonly Officer[] officers;
        private readonly PatrolCar[] patrolCars = new PatrolCar[3];
        private readonly GameObject[] roadblocks = new GameObject[2];
        private Transform helicopter;
        private Transform heliRotor;
        private Light heliSpotlight;
        private AudioClip shotClip;

        private float nextAiTick;
        private float searchTimer;
        private float hideTimer;

        internal Vector3 LastKnownPosition { get; private set; }
        internal bool PlayerSpotted { get; private set; }
        internal float EscapeProgress { get; private set; }
        internal int DamageThisFrame { get; private set; }
        internal Vector3? DamageSource { get; private set; }
        internal float ArrestProgress { get; private set; }
        internal string StatusBanner { get; private set; } = "";

        private const float ArrestDistance = 1.85f;
        private const float ArrestSeconds = 1.35f;

        internal CostaPoliceSystem(HarborBlockout art, Transform parent, CostaEffects effects, Vector3[] posts)
        {
            this.art = art;
            this.effects = effects;
            root = new GameObject("Tactical police system").transform;
            root.SetParent(parent, false);
            officers = new Officer[posts.Length + 4];
            for (int i = 0; i < officers.Length; i++)
                officers[i] = CreateOfficer(i, i < posts.Length ? posts[i] : posts[i % posts.Length], i >= posts.Length);

            BuildPatrolCars();
            BuildRoadblocks();
            BuildHelicopter();
            shotClip = HarborArsenal.MakeShot("Tactical police", 0);
        }

        private Officer CreateOfficer(int index, Vector3 post, bool elite)
        {
            Color tint = elite ? new Color(0.22f, 0.25f, 0.30f) : new Color(0.51f, 0.59f, 0.62f);
            HarborCharacter character = art.Person($"Police unit {index + 1}", root, post, tint);
            Transform model = character.Root;
            art.PoliceUniform(character);

            NavMeshAgent agent = model.gameObject.AddComponent<NavMeshAgent>();
            agent.speed = elite ? 6.8f : 5.9f;
            agent.acceleration = 16f;
            agent.angularSpeed = 720f;
            agent.radius = 0.42f;
            agent.height = 1.8f;
            agent.stoppingDistance = 0.7f;

            CapsuleCollider hitbox = model.gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = Vector3.up * 0.91f;
            hitbox.radius = 0.33f;
            hitbox.height = 1.82f;
            hitbox.isTrigger = true;

            GameObject flash = art.Part(model, PrimitiveType.Sphere, "Officer muzzle flash",
                new Vector3(0.16f, 1.32f, 0.48f), new Vector3(0.14f, 0.14f, 0.24f), HarborBlockout.Amber);
            flash.SetActive(false);

            return new Officer
            {
                Character = character, Agent = agent, Hitbox = hitbox, Flash = flash,
                Post = post, MaxHealth = elite ? 110 : 70, Health = elite ? 110 : 70,
                MagSize = elite ? 14 : 8, Ammo = elite ? 14 : 8, Elite = elite
            };
        }

        private void BuildPatrolCars()
        {
            for (int i = 0; i < patrolCars.Length; i++)
            {
                GameObject car = new GameObject($"Police cruiser {i + 1}");
                car.transform.SetParent(root, false);
                art.Part(car.transform, PrimitiveType.Cube, "Cruiser body", new Vector3(0f, 0.55f, 0f),
                    new Vector3(1.92f, 0.72f, 4.35f), new Color(0.14f, 0.19f, 0.26f), true);
                art.Part(car.transform, PrimitiveType.Cube, "Cruiser cabin", new Vector3(0f, 1.12f, -0.15f),
                    new Vector3(1.62f, 0.54f, 2.15f), new Color(0.88f, 0.90f, 0.92f), true);
                GameObject red = art.Part(car.transform, PrimitiveType.Cube, "Lightbar red",
                    new Vector3(-0.34f, 1.44f, -0.1f), new Vector3(0.42f, 0.12f, 0.25f), new Color(0.95f, 0.18f, 0.18f));
                GameObject blue = art.Part(car.transform, PrimitiveType.Cube, "Lightbar blue",
                    new Vector3(0.34f, 1.44f, -0.1f), new Vector3(0.42f, 0.12f, 0.25f), new Color(0.18f, 0.52f, 0.98f));
                car.SetActive(false);
                patrolCars[i] = new PatrolCar { Root = car.transform, Beacons = new[] { red, blue } };
            }
        }

        private void BuildRoadblocks()
        {
            Vector3[] positions = { new(-30f, 0f, 165f), new(-30f, 0f, 52f) };
            for (int i = 0; i < roadblocks.Length; i++)
            {
                GameObject block = new GameObject($"Police roadblock {i + 1}");
                block.transform.SetParent(root, false);
                block.transform.position = positions[i];
                for (int side = -1; side <= 1; side += 2)
                {
                    art.Part(block.transform, PrimitiveType.Cube, "Barrier",
                        new Vector3(side * 4.2f, 0.55f, 0f), new Vector3(3.6f, 1.1f, 0.55f),
                        new Color(0.86f, 0.45f, 0.18f), true);
                }
                block.SetActive(false);
                roadblocks[i] = block;
            }
        }

        private void BuildHelicopter()
        {
            GameObject heli = new GameObject("Police helicopter");
            heli.transform.SetParent(root, false);
            art.Part(heli.transform, PrimitiveType.Cube, "Fuselage", Vector3.zero,
                new Vector3(1.8f, 1.4f, 4.8f), new Color(0.16f, 0.21f, 0.27f));
            art.Part(heli.transform, PrimitiveType.Cube, "Tail", new Vector3(0f, 0.2f, -3.6f),
                new Vector3(0.35f, 0.45f, 3.2f), new Color(0.16f, 0.21f, 0.27f));
            heliRotor = art.Part(heli.transform, PrimitiveType.Cube, "Main rotor", new Vector3(0f, 0.95f, 0f),
                new Vector3(7.4f, 0.08f, 0.35f), new Color(0.32f, 0.36f, 0.40f)).transform;
            heliSpotlight = new GameObject("Heli searchlight").AddComponent<Light>();
            heliSpotlight.transform.SetParent(heli.transform, false);
            heliSpotlight.transform.localPosition = new Vector3(0f, -0.75f, 1.2f);
            heliSpotlight.transform.localRotation = Quaternion.Euler(78f, 0f, 0f);
            heliSpotlight.type = LightType.Spot;
            heliSpotlight.range = 65f;
            heliSpotlight.spotAngle = 42f;
            heliSpotlight.intensity = 10f;
            heliSpotlight.color = new Color(0.92f, 0.96f, 1f);
            heliSpotlight.shadows = LightShadows.None;
            heli.SetActive(false);
            helicopter = heli.transform;
        }

        internal void ResetAll()
        {
            ArrestProgress = 0f;
            EscapeProgress = 0f;
            PlayerSpotted = false;
            StatusBanner = "";
            searchTimer = 0f;
            hideTimer = 0f;
            DamageThisFrame = 0;
            DamageSource = null;

            for (int i = 0; i < officers.Length; i++)
            {
                Officer o = officers[i];
                o.Health = o.MaxHealth;
                o.Ammo = o.MagSize;
                o.ReloadUntil = 0f;
                o.FireCooldown = 0.5f + (i % 3) * 0.25f;
                o.Flash.SetActive(false);
                bool baseGuard = !o.Elite;
                o.Agent.gameObject.SetActive(baseGuard);
                if (baseGuard)
                {
                    if (NavMesh.SamplePosition(o.Post, out NavMeshHit hit, 2.8f, NavMesh.AllAreas))
                        o.Agent.Warp(hit.position);
                    if (o.Agent.isOnNavMesh)
                        o.Agent.ResetPath();
                }
            }
            for (int i = 0; i < patrolCars.Length; i++)
            {
                patrolCars[i].Active = false;
                patrolCars[i].Speed = 0;
                patrolCars[i].Root.gameObject.SetActive(false);
            }
            for (int i = 0; i < roadblocks.Length; i++)
                roadblocks[i].SetActive(false);
            helicopter.gameObject.SetActive(false);
        }

        internal void SetActiveForOnline(bool online)
        {
            root.gameObject.SetActive(!online);
        }

        internal void Release()
        {
            foreach (Officer officer in officers)
                officer.Character.Release();
            Object.Destroy(shotClip);
        }

        internal bool TryHit(Collider hit, int damage)
        {
            for (int i = 0; i < officers.Length; i++)
            {
                Officer o = officers[i];
                if (!o.Agent.gameObject.activeSelf || o.Hitbox != hit)
                    continue;
                o.Health -= damage;
                if (o.Health <= 0)
                    o.Agent.gameObject.SetActive(false);
                return true;
            }
            return false;
        }

        internal void OnCrimeCommitted(Vector3 playerPos, ref int wantedLevel, bool lethal)
        {
            LastKnownPosition = playerPos;
            PlayerSpotted = true;
            hideTimer = 0f;
            int minLevel = lethal ? 2 : 1;
            if (wantedLevel < minLevel)
                wantedLevel = minLevel;
            else if (wantedLevel < 5)
                wantedLevel++;
            EnsureReinforcements(wantedLevel, playerPos, relocate: true);
        }

        private void EnsureReinforcements(int wantedLevel, Vector3 playerPos, bool relocate = false)
        {
            int desiredOfficers = wantedLevel switch
            {
                1 => 4,
                2 => 6,
                3 => 7,
                4 => 9,
                _ => officers.Length
            };
            for (int i = 0; i < officers.Length; i++)
            {
                Officer o = officers[i];
                bool distant = (o.Agent.transform.position - playerPos).sqrMagnitude > 95f * 95f;
                if (i < desiredOfficers && o.Health > 0 && (!o.Agent.gameObject.activeSelf || relocate && distant)
                    && TryFindSafeSpawn(playerPos, 28f, 42f, out Vector3 spawn))
                {
                    o.Agent.gameObject.SetActive(true);
                    o.Agent.Warp(spawn);
                }
            }

            int desiredCars = wantedLevel >= 3 ? Mathf.Min(patrolCars.Length, wantedLevel - 2) : 0;
            for (int i = 0; i < patrolCars.Length; i++)
            {
                bool shouldBeActive = i < desiredCars;
                if (shouldBeActive && !patrolCars[i].Active && TryFindSafeSpawn(playerPos, 42f, 62f, out Vector3 spawn))
                {
                    patrolCars[i].Root.position = spawn;
                    patrolCars[i].Root.LookAt(new Vector3(playerPos.x, spawn.y, playerPos.z));
                    patrolCars[i].Active = true;
                    patrolCars[i].Root.gameObject.SetActive(true);
                }
                else if (!shouldBeActive && patrolCars[i].Active)
                {
                    patrolCars[i].Active = false;
                    patrolCars[i].Root.gameObject.SetActive(false);
                }
            }

            for (int i = 0; i < roadblocks.Length; i++)
                roadblocks[i].SetActive(wantedLevel >= 4);

            bool useHeli = wantedLevel >= 5;
            if (useHeli && !helicopter.gameObject.activeSelf)
            {
                helicopter.position = playerPos + new Vector3(-35f, 26f, -35f);
                helicopter.gameObject.SetActive(true);
            }
            else if (!useHeli && helicopter.gameObject.activeSelf)
                helicopter.gameObject.SetActive(false);
        }

        private static bool TryFindSafeSpawn(Vector3 playerPos, float minR, float maxR, out Vector3 spawn)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(minR, maxR);
                Vector3 candidate = new(playerPos.x + Mathf.Cos(angle) * dist, 0f, playerPos.z + Mathf.Sin(angle) * dist);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 9f, NavMesh.AllAreas)
                    && Mathf.Abs(hit.position.y) < .8f)
                {
                    spawn = hit.position;
                    return true;
                }
            }
            spawn = default;
            return false;
        }

        internal bool Update(
            Vector3 playerPos,
            bool inVehicle,
            float vehicleSpeed,
            bool playerInvulnerable,
            ref int wantedLevel,
            out bool arrested)
        {
            DamageThisFrame = 0;
            DamageSource = null;
            arrested = false;

            if (wantedLevel <= 0)
            {
                AnimateIdle();
                return false;
            }

            float dt = Time.deltaTime;
            bool tickAi = Time.time >= nextAiTick;
            if (tickAi)
            {
                nextAiTick = Time.time + 0.22f;
                EnsureReinforcements(wantedLevel, playerPos);
            }

            bool anySaw = false;
            bool closeArrest = false;
            Vector3 eyeTarget = playerPos + Vector3.up * (inVehicle ? 1.6f : 1.25f);

            for (int i = 0; i < officers.Length; i++)
            {
                Officer o = officers[i];
                if (!o.Agent.gameObject.activeSelf || !o.Agent.isOnNavMesh)
                    continue;

                if (o.Flash.activeSelf && Time.time >= o.FlashUntil)
                    o.Flash.SetActive(false);

                Vector3 oPos = o.Agent.transform.position;
                Vector3 oEye = oPos + Vector3.up * 1.45f;
                float distSq = (oPos - playerPos).sqrMagnitude;
                bool hasLos = distSq < 46f * 46f && !Physics.Linecast(oEye, eyeTarget,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

                if (hasLos)
                {
                    anySaw = true;
                    LastKnownPosition = playerPos;
                }

                bool armed = wantedLevel >= 2;
                float combatDist = armed ? (o.Elite ? 14f : 11f) : 0.8f;

                if (tickAi)
                {
                    Vector3 goal = hasLos ? playerPos : LastKnownPosition;
                    if (armed && hasLos && distSq < combatDist * combatDist && distSq > 5.5f * 5.5f)
                    {
                        // Strafe laterally for cover/movement instead of running into the player
                        Vector3 toPlayer = (playerPos - oPos).normalized;
                        Vector3 strafe = Vector3.Cross(Vector3.up, toPlayer) * o.StrafeDir * 3.2f;
                        Vector3 tactical = oPos + strafe;
                        if (NavMesh.SamplePosition(tactical, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                            o.Agent.SetDestination(hit.position);
                        if (Random.value < 0.25f)
                            o.StrafeDir = -o.StrafeDir;
                    }
                    else
                    {
                        if (NavMesh.SamplePosition(goal, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
                            o.Agent.SetDestination(hit.position);
                    }
                    o.Agent.speed = Mathf.Lerp(5.4f, o.Elite ? 7.2f : 6.5f, (wantedLevel - 1f) / 4f);
                }

                // Face player during combat
                if (hasLos)
                {
                    Vector3 lookDir = Vector3.ProjectOnPlane(playerPos - oPos, Vector3.up);
                    if (lookDir.sqrMagnitude > 0.05f)
                        o.Agent.transform.rotation = Quaternion.RotateTowards(
                            o.Agent.transform.rotation, Quaternion.LookRotation(lookDir), dt * 360f);
                }

                // Armed fire logic (Wanted 2+)
                if (armed && hasLos && distSq <= 34f * 34f && distSq >= 2.2f * 2.2f)
                {
                    if (o.ReloadUntil > 0f)
                    {
                        if (Time.time >= o.ReloadUntil)
                        {
                            o.Ammo = o.MagSize;
                            o.ReloadUntil = 0f;
                        }
                    }
                    else if (Time.time >= o.FireCooldown)
                    {
                        float rate = o.Elite ? 0.38f : Mathf.Lerp(0.95f, 0.52f, (wantedLevel - 2f) / 3f);
                        o.FireCooldown = Time.time + rate;
                        o.Ammo--;
                        if (o.Ammo <= 0)
                            o.ReloadUntil = Time.time + 1.85f;

                        o.Flash.SetActive(true);
                        o.FlashUntil = Time.time + 0.06f;
                        if (shotClip != null)
                            effects.PlayShot(shotClip, oEye, .42f);

                        // Balanced inaccuracy: lower accuracy when player is moving fast or in vehicle
                        float hitChance = o.Elite ? 0.44f : 0.28f;
                        if (inVehicle && vehicleSpeed > 6f)
                            hitChance *= 0.45f;
                        if (!playerInvulnerable && Random.value < hitChance)
                        {
                            int dmg = o.Elite ? 11 : 7;
                            DamageThisFrame += dmg;
                            DamageSource = oEye;
                        }
                    }
                }

                // Close arrest check (especially Wanted 1 or on foot)
                if (hasLos && !playerInvulnerable && distSq <= ArrestDistance * ArrestDistance && (!inVehicle || vehicleSpeed < 1.5f))
                    closeArrest = true;

                // Animate character
                float speed = o.Agent.velocity.magnitude;
                o.Character.Animate(speed > 0.25f ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Idle,
                    Mathf.Clamp(speed / 5f, 0.6f, 1.15f));
                if (armed && hasLos)
                    o.Character.HoldWeapon(true, 0f, false);
            }

            UpdatePatrolCars(dt);
            UpdateHelicopter(eyeTarget, ref anySaw, dt);

            if (closeArrest)
            {
                ArrestProgress += dt / ArrestSeconds;
                if (ArrestProgress >= 1f)
                {
                    arrested = true;
                    return true;
                }
            }
            else
            {
                ArrestProgress = Mathf.Max(0f, ArrestProgress - dt * 0.8f);
            }

            // Search & Escape logic
            if (anySaw)
            {
                PlayerSpotted = true;
                hideTimer = 0f;
                searchTimer = 0f;
                EscapeProgress = 0f;
                StatusBanner = L("POLICE IN PURSUIT", "POLÍCIA NA COLA");
            }
            else
            {
                PlayerSpotted = false;
                hideTimer += dt;
                float escapeDuration = 8f + wantedLevel * 4f;
                EscapeProgress = Mathf.Clamp01(hideTimer / escapeDuration);
                StatusBanner = L("HIDDEN · EVADING", "OCULTO · DESPISTANDO");
                if (hideTimer >= escapeDuration)
                {
                    wantedLevel = 0;
                    hideTimer = 0f;
                    EscapeProgress = 0f;
                    StatusBanner = "";
                    ResetAll();
                    return false;
                }
            }

            searchTimer += dt;
            return true;
        }

        private void UpdatePatrolCars(float dt)
        {
            bool flashRed = Mathf.Repeat(Time.time * 4.5f, 1f) < 0.5f;
            for (int i = 0; i < patrolCars.Length; i++)
            {
                PatrolCar car = patrolCars[i];
                if (!car.Active)
                    continue;
                car.Beacons[0].SetActive(flashRed);
                car.Beacons[1].SetActive(!flashRed);

                Vector3 toTarget = LastKnownPosition - car.Root.position;
                toTarget.y = 0f;
                float dist = toTarget.magnitude;
                if (dist > 11f)
                {
                    Vector3 target = toTarget.normalized;
                    if (Physics.SphereCast(car.Root.position + Vector3.up * .72f, .84f, target,
                        out _, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        Vector3 left = Quaternion.Euler(0, -55, 0) * target;
                        target = Physics.SphereCast(car.Root.position + Vector3.up * .72f, .84f, left,
                            out _, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                            ? Quaternion.Euler(0, 55, 0) * target : left;
                    }
                    Quaternion desired = Quaternion.LookRotation(target);
                    car.Root.rotation = Quaternion.RotateTowards(car.Root.rotation, desired, dt * 85f);
                    car.Speed = Mathf.MoveTowards(car.Speed, 15.5f, dt * 11f);
                    Vector3 direction = car.Root.forward;
                    float travel = car.Speed * dt;
                    if (Physics.BoxCast(car.Root.position + Vector3.up * .66f + direction * 2.2f,
                        new Vector3(.92f, .45f, .08f), direction, out RaycastHit obstacle, car.Root.rotation,
                        travel + .15f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        travel = Mathf.Max(0, obstacle.distance - .15f);
                        car.Speed = 0;
                    }
                    car.Root.position += direction * travel;
                }
                else
                {
                    car.Speed = Mathf.MoveTowards(car.Speed, 0f, dt * 16f);
                }
            }
        }

        private void UpdateHelicopter(Vector3 playerEye, ref bool anySaw, float dt)
        {
            if (!helicopter.gameObject.activeSelf)
                return;
            heliRotor.Rotate(Vector3.up, dt * 1450f, Space.Self);
            Vector3 desired = LastKnownPosition + new Vector3(Mathf.Sin(Time.time * 0.45f) * 16f, 25f, Mathf.Cos(Time.time * 0.45f) * 16f);
            helicopter.position = Vector3.Lerp(helicopter.position, desired, dt * 1.4f);
            Vector3 look = LastKnownPosition - helicopter.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.5f)
                helicopter.rotation = Quaternion.Slerp(helicopter.rotation, Quaternion.LookRotation(look), dt * 2f);

            Transform light = heliSpotlight.transform;
            light.rotation = Quaternion.LookRotation(LastKnownPosition + Vector3.up - light.position);
            Vector3 beam = playerEye - light.position;
            if (beam.sqrMagnitude <= heliSpotlight.range * heliSpotlight.range
                && Vector3.Dot(light.forward, beam.normalized) >= Mathf.Cos(heliSpotlight.spotAngle * .5f * Mathf.Deg2Rad)
                && !Physics.Linecast(light.position, playerEye, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                anySaw = true;
                LastKnownPosition = new Vector3(playerEye.x, LastKnownPosition.y, playerEye.z);
            }
        }

        private void AnimateIdle()
        {
            for (int i = 0; i < officers.Length; i++)
            {
                Officer o = officers[i];
                if (!o.Agent.gameObject.activeSelf)
                    continue;
                if ((o.Agent.transform.position - o.Post).sqrMagnitude > 2.2f && o.Agent.isOnNavMesh)
                    o.Agent.SetDestination(o.Post);
                float speed = o.Agent.velocity.magnitude;
                o.Character.Animate(speed > 0.25f ? HarborCharacter.Motion.Walk : HarborCharacter.Motion.Idle, 0.7f);
            }
        }
    }
}
