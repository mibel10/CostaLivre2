using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace CostaLivre
{
    internal sealed partial class CostaInteriors
    {
        internal int LoadedFloors { get; private set; }

        internal void Stream(Vector3 viewer, float hour, bool running, bool low, float audioVolume)
        {
            float now = Time.unscaledTime;
            if (running || stream != null)
            {
                if (stream != null && !stream.MoveNext())
                {
                    loading.Ready = true;
                    LoadedFloors++;
                    stream = null;
                    loading = null;
                }
                if (running && now >= nextSurvey)
                {
                    nextSurvey = now + .18f;
                    Survey(viewer, low, now);
                }
            }

            if (now >= nextEviction)
            {
                nextEviction = now + .32f;
                foreach (Building building in buildings)
                {
                    RoomSet stale = null;
                    foreach (RoomSet room in building.Rooms)
                    {
                        if (room.Ready && now - room.LastNeeded > (low ? 5f : 12f))
                        {
                            stale = room;
                            break;
                        }
                    }
                    if (stale == null)
                        continue;
                    Evict(stale);
                    break;
                }
            }

            bool rethink = now >= nextNpc;
            if (rethink)
                nextNpc = now + .28f;
            for (int b = 0; b < buildings.Length; b++)
            {
                var building = buildings[b];
                for (int level = 0; level < building.Rooms.Length; level++)
                {
                    RoomSet room = building.Rooms[level];
                    if (!room.Ready)
                        continue;
                    float distance = (viewer - building.Plan.Service(level, 0)).sqrMagnitude;
                    bool active = running && distance < (low ? 14 * 14 : 22 * 22)
                        && Mathf.Abs(viewer.y - building.Plan.Y(level)) < 2.2f;
                    if (rethink)
                    {
                        for (int zone = 0; zone < room.Lights.Length; zone++)
                            room.Lights[zone].enabled = active && (States[b].lights & (1 << (level*2+zone))) != 0;
                        if (room.Screen != null)
                            room.Screen.enabled = (States[b].computers & (1 << level)) != 0;
                        if (active && building.Plan.IsOpen(hour) && level == RoutineFloor(building.Plan, hour))
                            UpdateVisitor(room, building.Plan, level, hour);
                        else
                            ReturnVisitor(room);
                    }
                    if (active && room.Visitor != null)
                    {
                        if (room.Visitor.Agent.velocity.sqrMagnitude > .01f)
                            room.Visitor.Body.AnimateDistance(HarborCharacter.Motion.Walk, distance, .75f);
                        else
                            room.Visitor.Body.AnimateDistance(HarborCharacter.Motion.Idle, distance);
                    }
                }
            }
            if (rethink)
                UpdateAmbience(viewer, running ? audioVolume : 0);
        }

        private void Survey(Vector3 viewer, bool low, float now)
        {
            float best = float.PositiveInfinity;
            Building candidate = null;
            int floor = 0;
            for (int i = 0; i < buildings.Length; i++)
            {
                Building building = buildings[i];
                State state = States[i];
                CostaInteriorPlan p = building.Plan;
                float dx = viewer.x - Mathf.Clamp(viewer.x, p.Left, p.Right);
                float dz = viewer.z - Mathf.Clamp(viewer.z, p.Front, p.Back);
                float horizontal = dx*dx + dz*dz;
                bool inside = p.Contains(viewer, .5f);
                int nearest = Mathf.Clamp(Mathf.FloorToInt((viewer.y - CostaInteriorPlan.Base + .16f) / CostaInteriorPlan.Rise), 0, p.Levels - 1);
                for (int level = 0; level < building.Rooms.Length; level++)
                {
                    RoomSet room = building.Rooms[level];
                    int above = Mathf.Abs(nearest - level);
                    bool want = inside ? above <= 1 : level == 0 && horizontal < (low ? 26 * 26 : 39 * 39)
                        && viewer.y < 5;
                    // While riding, warm the destination before the doors open.
                    if (inside && state.moving && state.target == level && p.InCabin(viewer, viewer.y))
                        want = true;
                    if (!want)
                        continue;
                    room.LastNeeded = now;
                    float weight = horizontal + above * 90f;
                    if (stream != null || room.Ready || weight >= best)
                        continue;
                    candidate = building;
                    floor = level;
                    best = weight;
                }
            }
            if (candidate == null)
                return;
            loading = candidate.Rooms[floor];
            stream = Dress(candidate, floor);
        }

        private IEnumerator Dress(Building building, int level)
        {
            CostaInteriorPlan p = building.Plan;
            RoomSet room = building.Rooms[level];
            room.Visuals = new GameObject("Interior " + p.Purpose + " " + (level + 1)).transform;
            room.Visuals.SetParent(root, false);
            room.Lights = new Light[2];
            float y = p.Y(level);
            for (int zone = 0; zone < 2; zone++)
            {
                Vector3 control = p.Service(level, zone);
                Decor(room, "Light switch", control + new Vector3(0,1.16f,.20f), new(.13f,.21f,.075f), Linen);
                Vector3 lampPoint = new(p.Entry.x + (zone == 0 ? -1 : 1) * p.Width * .23f, y+2.95f,
                    p.Front + p.Depth * (zone == 0 ? .34f : .67f));
                Decor(room, "Ceiling luminaire", lampPoint, new(.96f,.09f,.33f), Linen);
                Light lamp = new GameObject("Room lighting").AddComponent<Light>();
                lamp.transform.SetParent(room.Visuals, false);
                lamp.transform.position = lampPoint + Vector3.down * .10f;
                lamp.transform.rotation = Quaternion.Euler(90,0,0);
                lamp.type = LightType.Spot;
                lamp.range = 8.5f;
                lamp.spotAngle = 151;
                lamp.innerSpotAngle = 120;
                lamp.intensity = 3.95f;
                lamp.color = p.Purpose == CostaInteriorPlan.Use.Hospital ? new(.86f,.96f,1f) : new(1f,.86f,.70f);
                lamp.shadows = LightShadows.None;
                lamp.enabled = false;
                room.Lights[zone] = lamp;
                yield return null;
            }

            if (building.Cab != null)
            {
                room.FloorNumber = Indicator(room.Visuals, p, level);
                building.IndicatorFloor = -2;
            }

            Vector3 chair = p.Chair(level);
            Decor(room, "Upholstered back", chair + new Vector3(0,.56f,.27f), new(.64f,.64f,.12f), p.Accent);
            Decor(room, "Chair frame", chair + Vector3.up * .09f, new(.56f,.18f,.53f), Metal);
            Vector3 screen = p.Computer(level) + new Vector3(.54f,1.28f,.61f);
            Decor(room, "Computer panel", screen, new(.48f,.33f,.09f), Metal);
            room.Screen = Decor(room, "Powered display", screen + Vector3.back * .055f, new(.42f,.26f,.022f), Glass);
            Decor(room, "Keyboard", screen + new Vector3(0,-.31f,-.20f), new(.30f,.025f,.10f), Metal);
            if (p.Purpose is CostaInteriorPlan.Use.Hotel or CostaInteriorPlan.Use.Apartment or CostaInteriorPlan.Use.Hospital)
            {
                Vector3 mirror = new(p.Right - .188f, p.Y(level) + 1.53f, p.Front + p.Depth * .30f);
                Decor(room, "Mirror frame", mirror, new Vector3(.06f, 1.15f, 1.24f), Metal);
                art.Mirrors?.Install(room.Visuals, room.Visuals.InverseTransformPoint(mirror + Vector3.left * .042f),
                    Vector3.left, new Vector2(1.12f, 1.03f));
            }
            if (level != 0 || !p.HasOriginalLobby)
            {
                yield return null;
                Furnish(room, p, level, false);
                yield return null;
                Furnish(room, p, level, true);
            }
        }

        private Renderer Decor(RoomSet room, string name, Vector3 point, Vector3 size, Color color)
        {
            Renderer renderer = art.Part(room.Visuals,PrimitiveType.Cube,name,point,size,color).GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        private void Furnish(RoomSet room, CostaInteriorPlan p, int floor, bool far)
        {
            Vector3 at = FurnitureAt(p,floor,far);
            Color accent = Color.Lerp(p.Accent, Plaster, (floor%3)*.15f);
            switch (p.Purpose)
            {
                case CostaInteriorPlan.Use.Hospital:
                    Decor(room,"Bed base",at+new Vector3(0,.43f,0),new(.93f,.18f,1.92f),Metal);
                    Decor(room,"Medical mattress",at+new Vector3(0,.60f,0),new(.92f,.17f,1.87f),Linen);
                    Decor(room,"Pillow",at+new Vector3(0,.73f,.63f),new(.67f,.11f,.39f),Linen);
                    Decor(room,"Bedside cabinet",at+new Vector3(.67f,.44f,.52f),new(.40f,.86f,.42f),accent);
                    Decor(room,"Patient monitor",at+new Vector3(.67f,1.04f,.52f),new(.34f,.29f,.095f),Glass);
                    break;
                case CostaInteriorPlan.Use.Hotel:
                case CostaInteriorPlan.Use.Apartment:
                    Decor(room,far?"Platform bed":"Low sofa",at+Vector3.up*.30f, far ? new(1.29f,.51f,1.88f) : new(1.45f,.59f,.70f),Oak);
                    Decor(room,"Fabric cushion",at+Vector3.up*.57f,far?new(1.25f,.15f,1.82f):new(1.19f,.14f,.60f),Linen);
                    Decor(room,far?"Headboard":"Sofa back",at+new Vector3(0,.70f,far?.89f:.25f),new(1.29f,.70f,.13f),accent);
                    Decor(room,"Oak side cabinet",at+new Vector3(.96f,.41f,.35f),new(.46f,.81f,.48f),Oak);
                    Decor(room,"Reading lamp",at+new Vector3(.96f,.97f,.35f),new(.27f,.29f,.27f),Linen);
                    break;
                case CostaInteriorPlan.Use.Garage:
                    Decor(room,far?"Tool drawers":"Work bench",at+Vector3.up*.43f,new(1.28f,.84f,.59f),accent);
                    Decor(room,"Workbench steel top",at+Vector3.up*.87f,new(1.36f,.08f,.66f),Metal);
                    for(int i=0;i<3;i++)
                        Decor(room,"Spare parts bins",at+new Vector3(-.43f+i*.40f,1.04f,0),new(.27f,.23f,.29f),Metal);
                    break;
                case CostaInteriorPlan.Use.Museum:
                    Decor(room,"Exhibition cabinet",at+Vector3.up*.45f,new(1.28f,.90f,.71f),Oak);
                    Decor(room,"Artifact mount",at+Vector3.up*.99f,new(.37f,.17f,.34f),Linen);
                    Decor(room,"Exhibit frame",at+new Vector3(0,1.44f,.40f),new(.89f,.85f,.10f),accent);
                    Decor(room,"Exhibit panel",at+new Vector3(0,1.44f,.33f),new(.73f,.71f,.03f),Linen);
                    break;
                default:
                    Decor(room,far?"Records and stock shelving":"Service desk",at+Vector3.up*.46f,
                        new(1.37f,.9f,.61f),far?Oak:accent);
                    for (int i=0;i<3;i++)
                        Decor(room, "Storage trays",at+new Vector3(-.46f+i*.43f,.98f,.04f),
                            new(.29f,.13f,.31f),i == 1 ? accent : Linen);
                    Decor(room,"Upper cabinet",at+new Vector3(0,1.55f,.26f),new(1.37f,.73f,.36f),Oak);
                    break;
            }
        }

        private static int RoutineFloor(CostaInteriorPlan p, float hour)
        {
            if (p.Levels == 1)
                return 0;
            if (p.Purpose == CostaInteriorPlan.Use.Apartment)
                return hour < 8 || hour >= 18 ? p.Levels - 1 : 0;
            return hour >= 11 && hour < 15 ? Mathf.Min(1, p.Levels - 1) : 0;
        }

        private void UpdateVisitor(RoomSet room, CostaInteriorPlan p, int level, float hour)
        {
            Vector3 entry = new(p.Entry.x+.20f,p.Y(level),p.Front+p.Depth*.31f);
            int phase = Mathf.FloorToInt(hour * 7 + p.Entry.x * .02f) & 3;
            if (room.Visitor == null)
            {
                if (visitorWardrobe != null)
                {
                    if (!visitorWardrobe.isDone)
                        return;
                    visitorTexture = visitorWardrobe.asset as Texture2D;
                    visitorWardrobe = null;
                }
                if (!NavMesh.SamplePosition(entry, out NavMeshHit point, .9f, NavMesh.AllAreas))
                    return;
                Visitor visitor = visitors.Count > 0 ? visitors.Pop() : CreateVisitor();
                visitor.Body.Root.position = point.position;
                visitor.Body.Root.gameObject.name = p.Purpose switch
                {
                    CostaInteriorPlan.Use.Apartment => "Morador",
                    CostaInteriorPlan.Use.Hotel => "Hóspede",
                    CostaInteriorPlan.Use.Hospital => "Equipe de atendimento",
                    CostaInteriorPlan.Use.Station => "Policial de plantão",
                    _ => "Funcionário"
                };
                visitor.Body.Root.gameObject.SetActive(true);
                if (!visitor.Agent.Warp(point.position))
                {
                    visitor.Body.Root.gameObject.SetActive(false);
                    visitors.Push(visitor);
                    return;
                }
                visitor.Destination = -1;
                bool uniform = p.Purpose == CostaInteriorPlan.Use.Station;
                if (uniform)
                    visitor.Uniform ??= art.PoliceUniform(visitor.Body);
                if (visitor.Uniform != null)
                    foreach (GameObject part in visitor.Uniform)
                        part.SetActive(uniform);
                room.Visitor = visitor;
            }
            Visitor npc = room.Visitor;
            if (phase == npc.Destination || !npc.Agent.isOnNavMesh)
                return;
            float z = p.Front + p.Depth * (phase < 2 ? .31f : .71f);
            Vector3 goal = new(p.Entry.x+(phase%2==0?-.78f:1.78f),p.Y(level),z);
            if (NavMesh.SamplePosition(goal, out NavMeshHit hit, .9f, NavMesh.AllAreas) && npc.Agent.SetDestination(hit.position))
                npc.Destination = phase;
        }

        private Visitor CreateVisitor()
        {
            HarborCharacter body = art.Person("Visitante", root, Vector3.zero, Color.white);
            body.WearTexture(visitorTexture);
            body.Root.gameObject.SetActive(false);
            NavMeshAgent agent = body.Root.gameObject.AddComponent<NavMeshAgent>();
            agent.radius = .30f;
            agent.speed = 1.36f;
            agent.acceleration = 3.6f;
            agent.angularSpeed = 270;
            agent.autoTraverseOffMeshLink = false;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            var visitor = new Visitor { Body = body, Agent = agent };
            allVisitors.Add(visitor);
            return visitor;
        }

        private void ReturnVisitor(RoomSet room)
        {
            if (room.Visitor == null)
                return;
            room.Visitor.Body.Root.gameObject.SetActive(false);
            visitors.Push(room.Visitor);
            room.Visitor = null;
        }

        private void Evict(RoomSet room)
        {
            ReturnVisitor(room);
            Object.Destroy(room.Visuals.gameObject);
            room.Ready = false;
            LoadedFloors--;
        }

        private void UpdateAmbience(Vector3 viewer, float volume)
        {
            int zone = -1;
            if (volume > 0)
                for (int i = 0; i < buildings.Length; i++)
                    if (buildings[i].Plan.Contains(viewer) && viewer.y < buildings[i].Plan.Height)
                    {
                        zone = i;
                        break;
                    }

            if (zone >= 0 && ambience == null)
            {
                ambience = new GameObject("Acústica interior").AddComponent<AudioSource>();
                ambience.transform.SetParent(root, false);
                ambience.loop = true;
                ambience.playOnAwake = false;
            }
            if (ambience == null)
                return;
            if (zone != ambienceZone)
            {
                ambienceZone = zone;
                ambience.Stop();
                ambience.clip = null;
            }
            if (zone < 0)
                return;
            if (ambience.clip == null)
            {
                int clip = buildings[zone].Plan.Purpose switch
                {
                    CostaInteriorPlan.Use.Garage => 2,
                    CostaInteriorPlan.Use.Hotel or CostaInteriorPlan.Use.Restaurant or CostaInteriorPlan.Use.Shop => 1,
                    _ => 0
                };
                if (ambienceRequests[clip].isDone && ambienceRequests[clip].asset is AudioClip asset)
                {
                    ambience.clip = asset;
                    ambience.Play();
                }
            }
            ambience.volume = volume * .12f;
        }

        internal void Release()
        {
            foreach (Visitor visitor in allVisitors)
                visitor.Body.Release();
            if (ambience != null)
                ambience.Stop();
        }
    }
}
