using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed partial class CostaInteriors
    {
        private const float StairWidth = 2.4f;
        private static readonly Color Plaster = new(.63f,.60f,.55f);
        private static readonly Color Metal = new(.22f,.26f,.28f);
        private static readonly Color Oak = new(.38f,.25f,.16f);
        private static readonly Color Glass = new(.24f,.39f,.43f);
        private static readonly Color Linen = new(.85f,.80f,.72f);
        private readonly HarborBlockout art;
        private readonly Transform root;
        private readonly Building[] buildings;
        private readonly Stack<Visitor> visitors = new();
        private readonly List<Visitor> allVisitors = new();
        private IEnumerator stream;
        private RoomSet loading;
        private float nextSurvey;
        private float nextNpc;
        private float nextEviction;
        private AudioSource ambience;
        private ResourceRequest[] ambienceRequests;
        private int ambienceZone = -1;

        private sealed class RoomSet
        {
            internal Transform Visuals;
            internal Light[] Lights;
            internal Renderer Screen;
            internal TextMesh FloorNumber;
            internal Visitor Visitor;
            internal bool Ready;
            internal float LastNeeded;
        }

        private sealed class Building
        {
            internal CostaInteriorPlan Plan;
            internal RoomSet[] Rooms;
            internal Transform[] Doors;
            internal Transform[] Gates;
            internal Transform[] RoomDoors;
            internal float[] RoomDoorVisual;
            internal Transform Cab;
            internal float DoorVisual;
            internal float CabVisual;
            internal float[] GateVisual;
            internal int DisplayFloor = -2;
            internal int IndicatorFloor = -2;
            internal TextMesh RoofIndicator;
        }

        private sealed class Visitor
        {
            internal HarborCharacter Body;
            internal NavMeshAgent Agent;
            internal int Destination = -1;
            internal GameObject[] Uniform;
        }

        internal CostaInteriors(HarborBlockout art, Transform parent)
        {
            this.art = art;
            root = new GameObject("Interiores de Santa Luz e região").transform;
            root.SetParent(parent, false);
            buildings = new Building[CostaInteriorPlan.All.Length];
            States = new State[buildings.Length];
            for (int i = 0; i < buildings.Length; i++)
            {
                CostaInteriorPlan plan = CostaInteriorPlan.All[i];
                buildings[i] = new Building { Plan = plan, Rooms = new RoomSet[plan.Levels] };
                for (int j = 0; j < plan.Levels; j++)
                    buildings[i].Rooms[j] = new RoomSet();
                States[i] = new State { lights = (1 << (plan.Levels * 2)) - 1, height = plan.Y(0) };
            }
        }

        internal IEnumerable<float> BuildStructure()
        {
            ambienceRequests = new[]
            {
                Resources.LoadAsync<AudioClip>("Harbor/CityAudio/Wind"),
                Resources.LoadAsync<AudioClip>("Harbor/CityAudio/Conversation2"),
                Resources.LoadAsync<AudioClip>("Harbor/CityAudio/DistantTraffic")
            };
            for (int i = 0; i < buildings.Length; i++)
            {
                Building building = buildings[i];
                CostaInteriorPlan p = building.Plan;
                BuildEntrance(building);
                building.RoomDoors = new Transform[p.Levels];
                building.RoomDoorVisual = new float[p.Levels];
                for (int f = 0; f < p.Levels; f++)
                {
                    float y = p.Y(f);
                    Vector3 center = new(p.Entry.x, y + 1.55f, (p.Front + p.Back) * .5f);
                    art.Acoustics.AddRoom(new Bounds(center, new(p.Width - .35f, 3.1f, p.Depth - .35f)));
                    BuildFloorObstacles(building, f);
                    if (p.Vertical)
                    {
                        BuildSlab(p, f + 1);
                        BuildStair(p, f);
                    }
                    yield return (i + (f + 1f) / p.Levels) / buildings.Length;
                }

                if (p.Vertical)
                    BuildElevator(building);
            }
            Physics.SyncTransforms();
        }

        private GameObject Solid(string name, Vector3 position, Vector3 size, Color color)
        {
            return art.Box(name, position, size, color, true, bake: true);
        }

        private void BuildFloorObstacles(Building building, int f)
        {
            CostaInteriorPlan p = building.Plan;
            if (p.HasPartition(f))
            {
                Vector3 door = p.RoomDoor(f);
                float start = p.Front+.31f;
                float end = p.Back-.30f;
                const float half = .78f;
                float small = door.z-half-start;
                float large = end-door.z-half;
                Solid("Room front partition", new(door.x,door.y+1.53f,start+small/2),
                    new(.17f,3.06f,small),Plaster);
                Solid("Room rear partition", new(door.x,door.y+1.53f,end-large/2),
                    new(.17f,3.06f,large),Plaster);
                Solid("Room door header", door+Vector3.up*2.67f,new(.22f,.79f,1.56f),Plaster);
                Transform hinge = new GameObject("Room door hinge").transform;
                hinge.SetParent(root,false);
                hinge.position = door+new Vector3(0,1.12f,half);
                art.Part(hinge,PrimitiveType.Cube,"Room door",new(0,0,-half),new(.09f,2.22f,1.52f),Oak,true);
                art.Part(hinge,PrimitiveType.Cube,"Room lever",new(-.075f,-.05f,-1.30f),new(.11f,.04f,.14f),Metal);
                building.RoomDoors[f] = hinge;
            }
            Vector3 chair = p.Chair(f);
            Solid("Seat upholstered base", chair + Vector3.up * .21f, new(.64f,.42f,.63f), p.Accent);
            Vector3 computer = p.Computer(f);
            Solid("Work surface", computer + new Vector3(.54f,.47f,.55f), new(.83f,.94f,.75f), Oak);
            if (f == 0 && p.HasOriginalLobby)
                return;
            foreach (bool far in new[] { false,true })
            {
                Vector3 at = FurnitureAt(p,f,far);
                Vector3 size = p.Purpose switch
                {
                    CostaInteriorPlan.Use.Hospital => new(.93f,.60f,1.92f),
                    CostaInteriorPlan.Use.Hotel or CostaInteriorPlan.Use.Apartment => far ? new(1.25f,.62f,1.84f) : new(1.40f,.65f,.70f),
                    _ => new(1.30f,.90f,.61f)
                };
                Vector3 center = at + Vector3.up * size.y/2;
                GameObject collision = art.Part(root, PrimitiveType.Cube,"Furniture collision",center,size,Oak,true);
                collision.GetComponent<Renderer>().enabled = false;
                art.AddNavigationBox(center,Quaternion.identity,size);
            }
        }

        private static Vector3 FurnitureAt(CostaInteriorPlan p, int floor, bool far) =>
            new(p.Entry.x + (far ? -1 : 1) * Mathf.Min(3.32f,p.Width*.30f),
                p.Y(floor),p.Front+p.Depth*(far ? .72f : .27f));

        private void BuildSlab(CostaInteriorPlan p, int level)
        {
            float y = p.Y(level);
            float stairLeft = p.StairX - StairWidth * .5f;
            float stairRight = p.StairX + StairWidth * .5f;
            void Slab(float x0, float x1, float z0, float z1)
            {
                if (x1 - x0 <= .04f || z1 - z0 <= .04f)
                    return;
                GameObject floor = Solid("Interior floor slab", new((x0+x1)/2,y-.11f,(z0+z1)/2),
                    new(x1-x0,.22f,z1-z0), level == p.Levels ? Metal : Linen);
                if (level < p.Levels)
                    art.MarkFootFloor(floor, p.Purpose is CostaInteriorPlan.Use.Apartment or CostaInteriorPlan.Use.Hotel
                        ? CostaAcousticScene.Floor.Wood : CostaAcousticScene.Floor.Stone);
            }

            Slab(p.Left, stairLeft, p.Front, p.Back);
            Slab(stairLeft, stairRight, p.Front, p.StairStart);
            Slab(stairLeft, stairRight, p.StairEnd - .035f, p.Back);
            Slab(stairRight, p.ShaftLeft, p.Front, p.Back);
            Slab(p.ShaftLeft, p.Right, p.Front, p.ShaftFront);
            Solid("Stairwell guard", new(stairRight,y+.49f,(p.StairStart+p.StairEnd)/2),
                new(.10f,.98f,p.StairEnd-p.StairStart), Metal);

            if (level != p.Levels)
                return;
            foreach (int side in new[] { -1, 1 })
            {
                Solid("Terrace parapet", new(p.Entry.x + side * (p.Width/2-.14f), y+.52f, (p.Front+p.Back)/2),
                    new(.28f,1.04f,p.Depth), p.Accent);
                Solid("Terrace parapet", new(p.Entry.x, y+.52f, (p.Front+p.Back)/2 + side * (p.Depth/2-.14f)),
                    new(p.Width,1.04f,.28f), p.Accent);
            }
        }

        private void BuildStair(CostaInteriorPlan p, int level)
        {
            float run = p.StairEnd - p.StairStart;
            float length = Mathf.Sqrt(run * run + CostaInteriorPlan.Rise * CostaInteriorPlan.Rise);
            Quaternion incline = Quaternion.Euler(-Mathf.Atan2(CostaInteriorPlan.Rise, run) * Mathf.Rad2Deg, 0, 0);
            Vector3 center = new(p.StairX, p.Y(level) + CostaInteriorPlan.Rise/2, (p.StairStart+p.StairEnd)/2);
            center -= incline * Vector3.up * .09f;
            Vector3 size = new(StairWidth, .18f, length + .025f);
            GameObject ramp = art.Part(root, PrimitiveType.Cube, "Stair walk surface", center, size, Oak, true);
            ramp.transform.rotation = incline;
            ramp.GetComponent<Renderer>().enabled = false;
            art.AddNavigationBox(center, incline, size);
            const int treads = 18;
            for (int i = 0; i < treads; i++)
            {
                float z = p.StairStart + (i+.5f)/treads * run;
                float y = p.Y(level) + (i+1f)/treads * CostaInteriorPlan.Rise;
                art.BatchDecoration(new(p.StairX, y-.115f, z), new(StairWidth,.23f,run/treads+.012f), Oak);
            }
            // A continuous rail closes the exposed side below the upper landing.
            Vector3 rail = center + new Vector3(StairWidth*.5f-.06f, .47f, 0);
            GameObject guard = art.Part(root, PrimitiveType.Cube, "Stair rail", rail, new(.11f, .83f, length), Metal, true);
            guard.transform.rotation = incline;
        }

        private void BuildEntrance(Building building)
        {
            CostaInteriorPlan p = building.Plan;
            Vector3 center = p.Entrance + Vector3.up * 1.29f;
            float span = p.DoorSpan;
            if (p.Door == CostaInteriorPlan.EntranceDoor.Swing)
            {
                Transform pivot = new GameObject("Entrance hinge").transform;
                pivot.SetParent(root, false);
                pivot.position = center + Vector3.left * (span / 2 - .035f);
                art.Part(pivot, PrimitiveType.Cube, "Hinged entrance leaf",
                    new(span/2-.035f,0,0), new(span-.065f,2.58f,.105f), p.Accent, true);
                building.Doors = new[] { pivot };
            }
            else if (p.Door == CostaInteriorPlan.EntranceDoor.Revolve)
            {
                Transform pivot = new GameObject("Revolving entrance rotor").transform;
                pivot.SetParent(root, false);
                pivot.position = center;
                art.Part(pivot, PrimitiveType.Cube, "Revolving glass wings", Vector3.zero,
                    new(span-.12f,2.57f,.085f), Glass, true, HarborBlockout.Finish.Glass);
                art.Part(pivot, PrimitiveType.Cylinder, "Revolving spindle", Vector3.zero,
                    new(.17f,1.28f,.17f), Metal, true);
                building.Doors = new[] { pivot };
            }
            else if (p.Door == CostaInteriorPlan.EntranceDoor.Shutter)
                building.Doors = new[] { art.Part(root, PrimitiveType.Cube, "Garage shutter", center,
                    new(span-.04f,2.57f,.12f), Metal, true).transform };
            else
                building.Doors = new[]
                {
                    art.Part(root, PrimitiveType.Cube, "Left sliding glass", center + Vector3.left * span/4,
                        new(span/2-.014f,2.57f,.085f), Glass, true, HarborBlockout.Finish.Glass).transform,
                    art.Part(root, PrimitiveType.Cube, "Right sliding glass", center + Vector3.right * span/4,
                        new(span/2-.014f,2.57f,.085f), Glass, true, HarborBlockout.Finish.Glass).transform
                };
        }

        private void BuildElevator(Building building)
        {
            CostaInteriorPlan p = building.Plan;
            float span = p.Right - p.ShaftLeft;
            float shaftZ = (p.ShaftFront + p.Back) * .5f;
            float sideHeight = p.Height + 2.84f;
            float x = p.Cabin(0).x;
            Solid("Lift shaft partition", new(p.ShaftLeft + .06f, sideHeight/2, shaftZ),
                new(.16f,sideHeight,p.Back-p.ShaftFront), Plaster);
            Solid("Lift shaft roof", new(x, sideHeight, shaftZ), new(span,.2f,p.Back-p.ShaftFront), Metal);
            Solid("Roof lift rear enclosure", new(x,p.Height+1.45f,p.Back-.18f), new(span,2.9f,.36f), Metal);
            Solid("Roof lift exterior enclosure", new(p.Right-.18f,p.Height+1.45f,shaftZ),
                new(.36f,2.9f,p.Back-p.ShaftFront), Metal);
            building.Gates = new Transform[p.Levels + 1];
            building.GateVisual = new float[p.Levels + 1];
            for (int f = 0; f <= p.Levels; f++)
            {
                float y = p.Y(f);
                building.Gates[f] = art.Part(root, PrimitiveType.Cube, "Lift landing gate", new(x,y+1.11f,p.ShaftFront),
                    new(2.34f,2.22f,.14f), Metal, true, HarborBlockout.Finish.Metal).transform;
                float leftBorder = x - 1.17f;
                float rightBorder = x + 1.17f;
                Solid("Lift jamb", new((p.ShaftLeft+leftBorder)/2,y+1.13f,p.ShaftFront),
                    new(leftBorder-p.ShaftLeft,2.26f,.22f), Plaster);
                Solid("Lift jamb", new((p.Right+rightBorder)/2,y+1.13f,p.ShaftFront),
                    new(p.Right-rightBorder,2.26f,.22f), Plaster);
                Solid("Lift header", new(x,y+2.68f,p.ShaftFront), new(span,.88f,.22f), Plaster);
                art.Part(root,PrimitiveType.Cube,"Elevator call button",p.Panel(f) + Vector3.up * 1.12f,
                    new(.18f,.25f,.09f), p.Accent);
            }

            building.RoofIndicator = Indicator(root,p,p.Levels);
            building.Cab = new GameObject("Lift car").transform;
            building.Cab.SetParent(root, false);
            building.CabVisual = p.Y(0);
            building.Cab.position = p.Cabin(p.Y(0));
            art.Part(building.Cab,PrimitiveType.Cube,"Cab platform",new(0,-.09f,-.23f),
                new(2.49f,.18f,3.72f), Metal, true, HarborBlockout.Finish.Metal);
            art.Part(building.Cab,PrimitiveType.Cube,"Cab rear wall",new(0,1.07f,1.45f),
                new(2.49f,2.15f,.10f), p.Accent, true);
            art.Part(building.Cab,PrimitiveType.Cube,"Cab ceiling",new(0,2.23f,-.10f),
                new(2.49f,.12f,3.28f), Linen, true);
            for (int side = -1; side <= 1; side += 2)
            {
                art.Part(building.Cab,PrimitiveType.Cube,"Cab side wall",new(side*1.20f,1.09f,-.1f),
                    new(.085f,2.17f,3.26f), p.Accent, true);
                art.Part(building.Cab,PrimitiveType.Cube,"Cab rail",new(side*1.12f,.98f,-.1f),
                    new(.09f,.07f,2.8f), Linen, false, HarborBlockout.Finish.Metal);
            }
            art.Part(building.Cab,PrimitiveType.Cube,"Cab controls",new(.80f,1.17f,1.38f),new(.32f,.36f,.10f),Metal);
        }
    }
}
