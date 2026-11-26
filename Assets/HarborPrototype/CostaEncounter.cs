using UnityEngine;
using UnityEngine.AI;
using static CostaLivre.CostaText;
using Action = CostaLivre.CostaJobs.Action;

namespace CostaLivre
{
    internal sealed class CostaEncounter
    {
        private sealed class Opponent
        {
            internal HarborCharacter Character;
            internal CapsuleCollider Hitbox;
            internal GameObject Flash;
            internal AudioSource Audio;
            internal int Health = 65;
            internal float AimInterval;
            internal float AimTime;
            internal float FlashTime;
        }

        private static readonly Vector3[] GuardOffsets =
        {
            new(-3.7f, 0, -0.6f), new(3.7f, 0, 0.6f), new(-2.7f, 0, 3.6f), new(3.4f, 0, 3.5f)
        };

        private readonly HarborBlockout art;
        private readonly CostaJobs jobs;
        private readonly CostaVehicle[] cars;
        private readonly Transform root;
        private AudioClip enemyShot;
        private GameObject scene;
        private GameObject[] loot;
        private Opponent[] opponents;
        private HarborCharacter driver;
        private HarborCharacter contact;
        private HarborCharacter fleeingDriver;
        private float fleeTime;
        private int remaining;
        private float breach;
        private const float BreachSeconds = 1.8f;

        internal int Health { get; private set; } = 100;
        internal bool Active => jobs.Active >= CostaJobs.OriginalCount;
        internal bool Combat => Active && jobs.Current.Mode == Action.Fight;
        internal bool CarryingLoot { get; private set; }

        internal CostaEncounter(HarborBlockout art, Transform parent, CostaJobs jobs, CostaVehicle[] cars)
        {
            this.art = art;
            this.jobs = jobs;
            this.cars = cars;
            root = new GameObject("Solo contract encounter").transform;
            root.SetParent(parent, false);
        }

        internal void Reset()
        {
            ClearFleeingDriver();
            Health = 100;
            CarryingLoot = false;
            SceneChanged();
        }

        internal void SetOnline(bool online)
        {
            root.gameObject.SetActive(!online);
            if (!online)
                UpdateDriver();
        }

        internal void Dispose()
        {
            ReleaseActors();
            fleeingDriver?.Release();
            Object.Destroy(enemyShot);
        }

        internal Vector3 Location(Vector3 player)
        {
            CostaJobs.Step step = jobs.Current;
            if (step.Mode == Action.Seize)
                return cars[step.Vehicle].Root.position;
            if (step.Mode == Action.Collect && loot != null)
            {
                Vector3 closest = step.Point;
                float distance = float.PositiveInfinity;
                foreach (GameObject item in loot)
                {
                    if (!item.activeSelf)
                        continue;
                    Vector3 point = item.transform.position;
                    float separation = (player - point).sqrMagnitude;
                    if (separation < distance)
                    {
                        distance = separation;
                        closest = point;
                    }
                }
                return closest;
            }
            if (step.Mode == Action.Fight && opponents != null)
            {
                Vector3 closest = step.Point;
                float distance = float.PositiveInfinity;
                foreach (Opponent opponent in opponents)
                {
                    if (opponent.Health <= 0)
                        continue;
                    Vector3 point = opponent.Character.Root.position;
                    float separation = (player - point).sqrMagnitude;
                    if (separation < distance)
                    {
                        distance = separation;
                        closest = point;
                    }
                }
                return closest;
            }
            return step.Point;
        }

        internal string Status => jobs.Current.Mode switch
        {
            Action.Fight or Action.Collect => $"{jobs.Current.Title} · {jobs.Current.Count - remaining}/{jobs.Current.Count}",
            _ => jobs.Current.Title
        };

        internal string Prompt(Vector3 player, string use)
        {
            CostaJobs.Step step = jobs.Current;
            if (!CostaJobs.Near(player, Location(player), step.Radius))
                return "";
            if (step.Mode == Action.Breach)
            {
                int percent = Mathf.FloorToInt(100 * breach / BreachSeconds);
                return L($"Hold {use} · {percent}%", $"Segure {use} · {percent}%");
            }
            if (step.Mode == Action.DeliverVehicle)
                return L("Bring the marked vehicle here", "Traga o veículo marcado até aqui");
            return ClaimsUse(player) ? $"{use} {step.Title}" : "";
        }

        internal bool ClaimsUse(Vector3 player)
        {
            CostaJobs.Step step = jobs.Current;
            if (step.Mode is Action.Fight or Action.DeliverVehicle)
                return false;
            Vector3 location = Location(player);
            return CostaJobs.Near(player, location, step.Radius)
                && Accessible(player, location, step.Mode == Action.Seize ? cars[step.Vehicle] : null);
        }

        internal bool ReservesVehicle(int index)
        {
            CostaJobs.Step[] steps = CostaJobs.List[jobs.Active].Steps;
            for (int i = jobs.StepNumber - 1; i < steps.Length; i++)
                if (steps[i].Mode == Action.Seize && steps[i].Vehicle == index)
                    return true;
            return false;
        }

        internal CostaVehicle Advance(Vector3 player, CostaVehicle driven, bool use, bool hold, bool invulnerable, float delta)
        {
            TickFlee(delta);
            if (!Active)
                return null;

            CostaJobs.Step step = jobs.Current;
            if (step.Mode == Action.Fight)
            {
                TickOpponents(player, invulnerable, delta);
                return null;
            }
            if (step.Mode == Action.DeliverVehicle)
            {
                if (driven == cars[step.Vehicle] && driven.Speed < 4f
                    && CostaJobs.Near(driven.Root.position, step.Point, step.Radius))
                    Complete();
                return null;
            }

            UpdateDriver();
            bool near = driven == null && ClaimsUse(player);
            if (step.Mode == Action.Breach)
            {
                breach = near && hold ? breach + delta : 0;
                if (breach >= BreachSeconds)
                    Complete();
                return null;
            }
            if (!use || !near)
                return null;

            if (step.Mode == Action.Collect)
            {
                Vector3 location = Location(player);
                foreach (GameObject item in loot)
                {
                    if (!item.activeSelf || item.transform.position != location)
                        continue;
                    item.SetActive(false);
                    CarryingLoot = true;
                    if (--remaining == 0)
                        Complete();
                    break;
                }
                return null;
            }
            if (step.Mode == Action.Seize)
            {
                fleeingDriver = driver;
                driver = null;
                fleeingDriver.Root.SetParent(root, true);
                fleeTime = 1.7f;
                Complete();
                return cars[step.Vehicle];
            }

            Complete();
            return null;
        }

        internal bool Hit(Collider hit, int damage)
        {
            if (!Combat || opponents == null)
                return false;
            foreach (Opponent opponent in opponents)
            {
                if (opponent.Hitbox != hit || opponent.Health <= 0)
                    continue;
                opponent.Health -= damage;
                if (opponent.Health <= 0)
                {
                    opponent.Character.Root.gameObject.SetActive(false);
                    if (--remaining == 0)
                        Complete();
                }
                return true;
            }
            return false;
        }

        private void Complete()
        {
            jobs.CompleteStep();
            if (!Active)
            {
                CarryingLoot = false;
                ClearFleeingDriver();
            }
            SceneChanged();
        }

        private void SceneChanged()
        {
            ReleaseActors();
            Remove(scene);
            loot = null;
            opponents = null;
            driver = null;
            contact = null;
            breach = 0;
            if (!Active)
                return;

            CostaJobs.Step step = jobs.Current;
            scene = new GameObject("Contract objective");
            scene.transform.SetParent(root, false);
            remaining = step.Count;
            switch (step.Mode)
            {
                case Action.Breach:
                    AddProp("Control cabinet", step.Point, new Vector3(0.75f, 0.85f, 0.48f), new Color(0.53f, 0.31f, 0.18f));
                    break;
                case Action.Collect:
                    loot = new GameObject[step.Count];
                    for (int i = 0; i < loot.Length; i++)
                    {
                        Vector3 point = step.Point + Vector3.right * ((i - (loot.Length - 1) * 0.5f) * 2.5f);
                        loot[i] = AddLoot(step.Loot, Walkable(point));
                    }
                    break;
                case Action.Fight:
                    opponents = new Opponent[step.Count];
                    for (int i = 0; i < opponents.Length; i++)
                        opponents[i] = CreateOpponent(Walkable(step.Point + GuardOffsets[i]), i);
                    break;
                case Action.Seize:
                    ClearFleeingDriver();
                    driver = art.Person("Contract driver", scene.transform, step.Point, new Color(0.58f, 0.48f, 0.30f));
                    UpdateDriver();
                    break;
                case Action.DeliverVehicle:
                    foreach (float side in new[] { -1f, 1f })
                        art.Part(scene.transform, PrimitiveType.Cube, "Drop-off marker",
                            step.Point + new Vector3(side * 2.4f, 0.025f, 0), new Vector3(0.14f, 0.045f, 6f), HarborBlockout.Mint);
                    break;
                case Action.Inspect:
                    contact = art.Person("Contract contact", scene.transform, Walkable(step.Point + Vector3.right * 0.8f),
                        new Color(0.27f, 0.42f, 0.40f));
                    contact.Animate(HarborCharacter.Motion.Idle);
                    break;
            }
        }

        private static Vector3 Walkable(Vector3 position) =>
            NavMesh.SamplePosition(position, out NavMeshHit hit, 2.5f, NavMesh.AllAreas) ? hit.position : position;

        private GameObject AddProp(string title, Vector3 point, Vector3 size, Color color)
        {
            GameObject prop = new GameObject(title);
            prop.transform.SetParent(scene.transform, false);
            prop.transform.position = point;
            art.Part(prop.transform, PrimitiveType.Cube, title, Vector3.up * (size.y * 0.5f), size, color);
            art.Part(prop.transform, PrimitiveType.Cube, "Latch", new Vector3(0, size.y * 0.7f, -size.z * 0.5f),
                new Vector3(0.23f, 0.09f, 0.035f), new Color(0.16f, 0.18f, 0.19f));
            return prop;
        }

        private GameObject AddLoot(CostaJobs.Haul kind, Vector3 point)
        {
            var (name, color, shape, size) = kind switch
            {
                CostaJobs.Haul.Bullion => ("Bullion", new Color(0.89f, 0.63f, 0.19f), PrimitiveType.Cube, new Vector3(0.46f, 0.13f, 0.24f)),
                CostaJobs.Haul.Gems => ("Gems", new Color(0.67f, 0.15f, 0.33f), PrimitiveType.Sphere, new Vector3(0.21f, 0.19f, 0.20f)),
                CostaJobs.Haul.Drives => ("Data module", HarborBlockout.Mint, PrimitiveType.Cube, new Vector3(0.41f, 0.14f, 0.22f)),
                CostaJobs.Haul.Records => ("Records", new Color(0.74f, 0.64f, 0.43f), PrimitiveType.Cube, new Vector3(0.39f, 0.11f, 0.28f)),
                CostaJobs.Haul.Artifact => ("Artifact", new Color(0.81f, 0.58f, 0.29f), PrimitiveType.Cylinder, new Vector3(0.25f, 0.17f, 0.23f)),
                _ => ("", HarborBlockout.Amber, PrimitiveType.Cube, Vector3.zero)
            };
            bool closed = kind == CostaJobs.Haul.Case;
            GameObject container = AddProp(closed ? "Marked case" : name + " display", point,
                new Vector3(0.69f, closed ? 0.35f : 0.64f, 0.49f), closed ? color : new Color(0.22f, 0.24f, 0.25f));
            if (!closed)
            {
                Vector3 position = Vector3.up * (0.64f + size.y * (shape == PrimitiveType.Cylinder ? 1 : 0.5f));
                art.Part(container.transform, shape, name, position, size, color);
            }
            return container;
        }

        private Opponent CreateOpponent(Vector3 point, int index)
        {
            HarborCharacter character = art.Person("Contract opponent", scene.transform, point, new Color(0.55f, 0.21f, 0.17f));
            CapsuleCollider hitbox = character.Root.gameObject.AddComponent<CapsuleCollider>();
            hitbox.center = Vector3.up * 0.91f;
            hitbox.radius = 0.32f;
            hitbox.height = 1.82f;
            hitbox.isTrigger = true;

            Transform mount = new GameObject("Weapon mount").transform;
            mount.SetParent(character.Root, false);
            character.Animate(HarborCharacter.Motion.Idle);
            character.HoldWeapon(true, 0, true);
            mount.position = character.WeaponHand;
            Transform muzzle = HarborArsenal.BuildModel(art, mount, 0);
            GameObject flash = art.WeaponFlash(muzzle).gameObject;
            flash.SetActive(false);
            AudioSource audio = character.Root.gameObject.AddComponent<AudioSource>();
            audio.spatialBlend = 1;
            audio.minDistance = 4;
            audio.maxDistance = 27;
            enemyShot ??= HarborArsenal.MakeShot("Contract guard", 0);
            return new Opponent
            {
                Character = character, Hitbox = hitbox, Flash = flash, Audio = audio, AimInterval = 1.8f + 0.15f * index
            };
        }

        private static bool Accessible(Vector3 from, Vector3 to, CostaVehicle allowed = null) =>
            !Physics.Linecast(from + Vector3.up, to + Vector3.up, out RaycastHit hit,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || (allowed != null && allowed.OwnsCollider(hit.collider));

        private void TickOpponents(Vector3 player, bool invulnerable, float delta)
        {
            foreach (Opponent opponent in opponents)
            {
                if (opponent.Health <= 0)
                    continue;
                Transform enemy = opponent.Character.Root;
                opponent.FlashTime -= delta;
                opponent.Flash.SetActive(opponent.FlashTime > 0);
                Vector3 toward = player - enemy.position;
                toward.y = 0;
                bool aiming = toward.sqrMagnitude < 22f * 22f && Accessible(enemy.position, player);
                if (!aiming)
                {
                    opponent.AimTime = 0;
                    continue;
                }
                if (toward.sqrMagnitude > 0.001f)
                    enemy.rotation = Quaternion.LookRotation(toward);
                opponent.AimTime += delta;
                if (opponent.AimTime < opponent.AimInterval)
                    continue;
                opponent.AimTime = 0;
                opponent.FlashTime = 0.07f;
                opponent.Audio.PlayOneShot(enemyShot, 0.26f);
                if (!invulnerable)
                    Health = Mathf.Max(0, Health - 6);
            }
        }

        private void UpdateDriver()
        {
            if (driver == null)
                return;
            CostaVehicle vehicle = cars[jobs.Current.Vehicle];
            driver.Root.SetPositionAndRotation(vehicle.Root.position - vehicle.Root.right * vehicle.ExitOffset,
                Quaternion.LookRotation(-vehicle.Root.right));
            driver.Animate(HarborCharacter.Motion.Idle);
        }

        private void TickFlee(float delta)
        {
            if (fleeingDriver == null)
                return;
            fleeTime -= delta;
            if (fleeTime <= 0)
            {
                ClearFleeingDriver();
                return;
            }
            Vector3 current = fleeingDriver.Root.position;
            Vector3 next = Walkable(current + fleeingDriver.Root.forward * (3.4f * delta));
            if (CostaJobs.Near(current, next, 0.6f))
                fleeingDriver.Root.position = next;
            fleeingDriver.Animate(HarborCharacter.Motion.Run, 3.4f);
        }

        private void ClearFleeingDriver()
        {
            if (fleeingDriver == null)
                return;
            fleeingDriver.Release();
            Remove(fleeingDriver.Root.gameObject);
            fleeingDriver = null;
        }

        private void ReleaseActors()
        {
            contact?.Release();
            driver?.Release();
            if (opponents != null)
                foreach (Opponent opponent in opponents)
                    opponent.Character.Release();
        }

        private static void Remove(GameObject item)
        {
            if (item == null)
                return;
            item.SetActive(false);
            Object.Destroy(item);
        }
    }
}
