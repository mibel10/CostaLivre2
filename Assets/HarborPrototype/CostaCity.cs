using System.Collections.Generic;
using UnityEngine;
using Floor = CostaLivre.CostaAcousticScene.Floor;

namespace CostaLivre
{
    internal sealed class CostaCity
    {
        internal static readonly Vector3 Hotel = new(-55, 0, 56);
        internal static readonly Vector3 Jeweler = new(-8, 0, 56);
        internal static readonly Vector3 Museum = new(-99, 0, 98);
        internal static readonly Vector3 Station = new(-54, 0, 99);
        internal static readonly Vector3 GunShop = new(10, 0, 57);
        internal static readonly Vector3 Electronics = new(-10, 0, 98);
        internal static readonly Vector3[] Vendors = { new(-23, 0, 45), new(-83, 0, 132) };
        internal static readonly Vector3 GunCounter = GunShop + new Vector3(0, 0, 2.4f);
        internal static readonly float[] Avenues = { -123, -79, -30, 18, 67 };
        internal static readonly float[] CrossStreets = { 29, 76, 123 };
        internal static readonly Vector3 ParkGate = new(42, 0, 90);
        internal static readonly Vector3 ParkWheel = new(48, 0, 120);
        internal static readonly Vector3 Gallery = new(-105, 0, 104);
        internal static readonly Vector3 HotelDrop = new(-51, 0, 57);
        internal static readonly Vector3[] Estate = { new(-102, 0, 53), new(-102, 0, 141), new(-54, 0, 141) };
        internal static readonly Vector3 CitySpawn = new(-29, 0, 28);

        internal const float HighwayX = -30;
        internal const float HighwayStart = 168;
        internal const float HighwayEnd = 786;
        internal const float HighwayWidth = 26;
        internal static readonly Rect HighwayArea = new(HighwayX - HighwayWidth / 2, HighwayStart,
            HighwayWidth, HighwayEnd - HighwayStart);

        internal sealed class District
        {
            internal readonly string Name;
            internal readonly float Center;
            internal readonly Rect Area;
            internal readonly Color Accent;

            internal District(string name, float center, Color accent)
            {
                Name = name;
                Center = center;
                Area = new Rect(-104, center - 65, 154, 130);
                Accent = accent;
            }
        }

        internal static readonly District[] Districts =
        {
            new("Calado City", 265, new Color(0.30f, 0.54f, 0.57f)),
            new("Antutu City", 495, new Color(0.35f, 0.45f, 0.64f)),
            new("Vice City", 721, new Color(0.83f, 0.36f, 0.52f))
        };

        internal static bool OnRegionalLand(Vector3 position)
        {
            Vector2 point = new(position.x, position.z);
            if (HighwayArea.Contains(point))
                return true;
            foreach (District district in Districts)
                if (district.Area.Contains(point))
                    return true;
            return false;
        }

        internal static string RegionName(Vector3 position)
        {
            Vector2 point = new(position.x, position.z);
            foreach (District district in Districts)
                if (district.Area.Contains(point))
                    return district.Name;
            return position.z >= HighwayStart ? CostaText.L("COASTAL HIGHWAY", "RODOVIA COSTEIRA") : "SANTA LUZ";
        }

        private static readonly Color Pavement = new(0.76f, 0.73f, 0.68f);
        private static readonly Color Stone = new(0.85f, 0.80f, 0.73f);
        private static readonly Color Glass = new(0.15f, 0.23f, 0.26f);
        private static readonly Color Brass = new(0.64f, 0.49f, 0.27f);
        private readonly HarborBlockout art;
        private readonly Transform root;
        private Transform wheel;
        private readonly Transform[] gondolas = new Transform[8];

        internal readonly List<Rect> Footprints = new();
        internal readonly GameObject[] HouseDoors = new GameObject[Estate.Length];
        private readonly Transform[] houseHinges = new Transform[Estate.Length];
        private readonly float[] houseDoorOpening = new float[Estate.Length];

        internal CostaCity(HarborBlockout art, Transform root)
        {
            this.art = art;
            this.root = root;
        }

        internal IEnumerable<float> Build()
        {
            Ground(-88.5f, -25, 127, 94);
            Ground(0, -43, 50, 42);
            Ground(-37, 95, 230, 146);
            Barrier(new(-152, 1.15f, 52), new(0.5f, 2.3f, 232));
            Edge(-152, HighwayX - HighwayWidth / 2, HighwayStart);
            Edge(HighwayX + HighwayWidth / 2, 78, HighwayStart);
            Barrier(new(78, 1.15f, 95), new(0.5f, 2.3f, 146));
            Barrier(new(-63.5f, 1.15f, -64), new(177, 2.3f, 0.5f));
            Barrier(new(25, 1.15f, -43), new(0.5f, 2.3f, 42));
            Barrier(new(51.5f, 1.15f, 22), new(53, 2.3f, 0.5f));

            foreach (float x in Avenues)
            {
                for (int z = 29; z < 165; z += 6)
                    if (!NearRoad(z, CrossStreets))
                        Mark(new(x, 0.018f, z), new(0.11f, 0.03f, 2.5f));
                for (int z = 35; z < 166; z += 24)
                    Lamp(new(x - 5, 0, z), Vector3.right);
            }

            foreach (float z in CrossStreets)
            {
                for (int x = -147; x < 76; x += 6)
                    if (!NearRoad(x, Avenues))
                        Mark(new(x, 0.018f, z), new(2.5f, 0.03f, 0.11f));
            }

            yield return 0.15f;

            Venue("GRAND HOTEL ATLÂNTICO", Hotel, 25, 16, 23, Stone, Brass);
            Venue("JOALHERIA ÁUREA", Jeweler, 18, 14, 6, new Color(0.52f, 0.54f, 0.50f), Brass);
            Venue("MUSEU DE SANTA LUZ", Museum, 27, 17, 7, Stone, new Color(0.32f, 0.36f, 0.34f));
            Venue("POLÍCIA • SANTA LUZ", Station, 23, 16, 8, new Color(0.65f, 0.71f, 0.77f),
                new Color(0.19f, 0.31f, 0.44f));

            Venue("ARMARIA SANTA LUZ", GunShop, 12, 11, 6.8f, new Color(0.56f, 0.62f, 0.60f), Glass);
            Venue("SANTA LUZ INFORMÁTICA", Electronics, 19, 16, 10.5f, new Color(0.73f, 0.77f, 0.80f),
                new Color(0.22f, 0.34f, 0.39f));
            Desk(GunShop + new Vector3(0, 0, 4.2f), Glass);
            Desk(Electronics + new Vector3(4.6f, 0, 2.3f), Glass);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 display = Electronics + new Vector3(-5.4f, 0, 5 + i * 2.1f);
                art.Box("Computer display table", display + Vector3.up * 0.48f, new Vector3(1.5f, 0.96f, 0.8f), Stone, true);
                art.Box("Monitor", display + Vector3.up * 1.19f, new Vector3(0.53f, 0.36f, 0.07f), Glass);
            }
            Desk(Hotel + new Vector3(4, 0, 2), Brass);
            Desk(Jeweler + new Vector3(-4, 0, 1), Glass);
            Desk(Station + new Vector3(4, 0, 2), Glass);
            for (int i = 0; i < 3; i++)
            {
                Vector3 spot = Museum + new Vector3(-7 + i * 6.6f, 0, 6);
                art.Box("Museum plinth", spot + Vector3.up * 0.50f, new Vector3(1.25f, 1, 1.25f), Stone, true);
                Shape("Sculpture", PrimitiveType.Sphere, spot + Vector3.up * 1.4f,
                    new Vector3(0.80f, 0.88f, 0.59f), Brass);
            }

            for (int i = 0; i < 3; i++)
            {
                Vector3 point = Electronics + new Vector3(-6.2f + i * .72f, .25f, 2.95f);
                GameObject crate = art.Box("Loose delivery crate", point, new Vector3(.45f, .50f, .42f),
                    new Color(.34f, .24f, .16f), true, bake: false);
                art.Damage.Mark(crate, CostaWorldDamage.Surface.Wood, 30);
            }

            yield return 0.35f;

            for (int i = 0; i < Estate.Length; i++)
            {
                string address = $"VILA {i + 1:00}";
                Venue(address, Estate[i], 16, 11, 4.3f, i == 1 ? Stone : new Color(0.74f, 0.66f, 0.54f), Glass, Floor.Wood);
                HouseDoors[i] = art.Box("Property entrance", Estate[i] + new Vector3(0, 1.27f, -2.03f),
                    new Vector3(2.65f, 2.54f, 0.22f), Glass, true, bake: false);
                Transform hinge = new GameObject("Property door hinge").transform;
                hinge.SetParent(root, false);
                hinge.position = HouseDoors[i].transform.position + Vector3.left * 1.325f;
                HouseDoors[i].transform.SetParent(hinge, true);
                houseHinges[i] = hinge;
                var obstacle = HouseDoors[i].AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                Desk(Estate[i] + new Vector3(3.4f, 0, 2.3f), Brass);
                Tree(Estate[i] + new Vector3(-9, 0, 2));
                Tree(Estate[i] + new Vector3(9, 0, 4));
            }

            for (int i = 0; i < Vendors.Length; i++)
            {
                Vector3 point = Vendors[i];
                art.Box("Loot exchange counter", point + new Vector3(0, 0.48f, 0.9f),
                    new Vector3(2.12f, 0.96f, 0.75f), Glass, true);
                art.Box("Loot exchange awning", point + new Vector3(0, 2.35f, 0.53f),
                    new Vector3(2.75f, 0.22f, 1.48f), Brass);
                art.Sign($"POSTO DE VENDA {i + 1}", point + new Vector3(0, 2.28f, -0.27f), 0.12f, Stone);
            }

            yield return 0.46f;

            BuildPark();
            DressBoulevard();
            for (int i = 0; i < 11; i++)
            {
                float x = -140 + i * 15;
                float height = 8f + i % 4 * 4.5f;
                if (i == 4 || i == 6)
                    continue;
                Block(new Vector3(x, 0, 158), 9, 9, height);
            }
            Block(new Vector3(31, 0, 58), 19, 19, 14);
            Block(new Vector3(32, 0, 151), 21, 12, 24);
            Block(new Vector3(-137, 0, 121), 13, 16, 13);
            Block(new Vector3(-55, 0, 118), 17, 7, 10);
            Venue("CAFÉ DA PRAÇA", new Vector3(-113, 0, 67), 12, 7, 5.3f, Stone, Brass);
            for (int i = 0; i < 4; i++)
                Tree(new Vector3(-18 + i * 8, 0, 38));
            Block(new Vector3(-140, 0, 65), 14, 19, 12);
            Block(new Vector3(-12, 0, 139), 22, 18, 17);
            Block(new Vector3(5, 0, -43), 16, 14, 8);
            for (int i = 0; i < 9; i++)
                Tree(new Vector3(-142 + 7.5f * i, 0, 32));
            Fountain(new Vector3(-56, 0, 39));
            yield return 0.58f;

            BuildHighway();
            for (int i = 0; i < Districts.Length; i++)
            {
                BuildDistrict(Districts[i], i);
                yield return 0.65f + 0.10f * i;
            }

            foreach (float part in art.FinishDecoration())
                yield return Mathf.Lerp(0.86f, 0.99f, part);
            yield return 1f;
        }

        private void Edge(float xMin, float xMax, float z)
        {
            Barrier(new Vector3((xMin + xMax) / 2, 1.15f, z), new Vector3(xMax - xMin, 2.3f, 0.5f));
        }

        private void BuildHighway()
        {
            // District ground spans the full width where the highway crosses town.
            float start = HighwayStart;
            foreach (District district in Districts)
            {
                float end = district.Area.yMin;
                float middle = (start + end) / 2;
                Ground(HighwayX, middle, HighwayWidth, end - start);
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = HighwayX + side * (HighwayWidth / 2 - 0.17f);
                    Barrier(new Vector3(x, 1.10f, middle), new Vector3(0.35f, 2.2f, end - start));
                }
                start = district.Area.yMax;
            }

            for (float z = HighwayStart + 3; z < HighwayEnd - 3; z += 8)
                Mark(new Vector3(HighwayX, 0.02f, z), new Vector3(0.16f, 0.03f, 4f));
            foreach (int side in new[] { -1, 1 })
                for (float z = HighwayStart + 8; z < HighwayEnd; z += 28)
                    Lamp(new Vector3(HighwayX + side * 10.5f, 0, z), Vector3.left * side);
            art.Sign("RODOVIA COSTEIRA", new Vector3(HighwayX, 4.1f, HighwayStart + 5), 0.28f, Stone);
        }

        private void BuildDistrict(District district, int index)
        {
            Rect area = district.Area;
            float z = district.Center;
            Ground(area.center.x, z, area.width, area.height);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side < 0 ? area.xMin : area.xMax;
                Barrier(new Vector3(x, 1.15f, z), new Vector3(0.45f, 2.3f, area.height));
            }
            Edge(area.xMin, HighwayArea.xMin, area.yMin);
            Edge(HighwayArea.xMax, area.xMax, area.yMin);
            if (index == Districts.Length - 1)
                Edge(area.xMin, area.xMax, area.yMax);
            else
            {
                Edge(area.xMin, HighwayArea.xMin, area.yMax);
                Edge(HighwayArea.xMax, area.xMax, area.yMax);
            }
            art.Sign(district.Name.ToUpperInvariant(),
                new Vector3(HighwayX, 4.2f, area.yMin + 6), 0.31f, district.Accent);
            art.Sign(district.Name.ToUpperInvariant(),
                new Vector3(HighwayX, 4.2f, area.yMax - 6), 0.31f, district.Accent, 180);

            for (int side = -1; side <= 1; side += 2)
            {
                float curbZ = z + side * 7.4f;
                Sidewalk(area.xMin + 2f, HighwayArea.xMin - 0.65f, curbZ, district.Accent);
                Sidewalk(HighwayArea.xMax + 0.65f, area.xMax - 2f, curbZ, district.Accent);
                for (float x = area.xMin + 12f; x < area.xMax - 8f; x += 27f)
                    if (Mathf.Abs(x - HighwayX) > 16f)
                        Lamp(new Vector3(x, 0, curbZ), Vector3.back * side);
            }

            for (float x = area.xMin + 4; x < area.xMax - 3; x += 7)
            {
                if (Mathf.Abs(x - HighwayX) < 10)
                    continue;
                Mark(new Vector3(x, 0.02f, z), new Vector3(3.5f, 0.03f, 0.13f));
            }

            for (int row = -1; row <= 1; row += 2)
                for (int block = 0; block < 6; block++)
                {
                    float x = block < 3 ? -91 + block * 16 : -4 + (block - 3) * 16;
                    float depth = 15 + (block % 3) * 2;
                    float height = index == 1 ? 11 + (block % 4) * 6 : 6 + (block % 3) * 3 + index;
                    Block(new Vector3(x, 0, z + row * 37), 11, depth, height);
                    Tree(new Vector3(x + 5, 0, z + row * 13));
                }

            switch (index)
            {
                case 0:
                    Venue("MERCADO DO CAIS", new Vector3(-69, 0, z + 9), 23, 16, 8, Stone, district.Accent);
                    for (int i = 0; i < 3; i++)
                        art.Box("Market stall", new Vector3(9 + i * 9, 0.55f, z - 13), new Vector3(3.8f, 1.1f, 2),
                            district.Accent, true);
                    break;
                case 1:
                    Venue("CENTRO TECNOLÓGICO", new Vector3(-73, 0, z + 9), 28, 17, 32, Stone, district.Accent);
                    Plaza(new Vector3(12, 0, z - 15), district.Accent);
                    break;
                default:
                    Venue("CLUBE DO MIRANTE", new Vector3(-72, 0, z + 9), 24, 17, 10, Stone, district.Accent);
                    Plaza(new Vector3(12, 0, z - 15), district.Accent);
                    break;
            }
        }

        private void Sidewalk(float left, float right, float z, Color accent)
        {
            float x = (left + right) * 0.5f;
            Vector3 center = new(x, .027f, z), size = new(right - left, .05f, 2.6f);
            art.BatchDecoration(center, size, Pavement);
            art.Acoustics.AddFloor(new Bounds(center, size), Floor.Stone);
            for (int side = -1; side <= 1; side += 2)
                art.BatchDecoration(new Vector3(x, 0.054f, z + side * 1.02f),
                    new Vector3(right - left, 0.012f, 0.055f), accent);
        }

        private void Plaza(Vector3 point, Color accent)
        {
            art.MarkFootFloor(art.Box("Plaza paving", point + Vector3.up * .03f, new(26, .06f, 15), Pavement), Floor.Stone);
            Fountain(point);
            for (int side = -1; side <= 1; side += 2)
            {
                art.Box("Plaza bench", point + new Vector3(side * 7, 0.36f, 0), new Vector3(3, 0.72f, 1),
                    accent, true);
                art.BatchDecoration(point + new Vector3(side * 7, 0.74f, 0),
                    new Vector3(3.1f, 0.095f, 1.02f), new Color(0.39f, 0.26f, 0.16f));
                art.BatchDecoration(point + new Vector3(0, 0.063f, side * 5.8f),
                    new Vector3(22f, 0.014f, 0.10f), accent);
            }
        }

        internal void UpdateHouseDoors(CostaJobs jobs, bool online, bool snap = false)
        {
            for (int i = 0; i < houseHinges.Length; i++)
            {
                float target = !online && jobs.Owned(i) ? 1 : 0;
                float current = houseDoorOpening[i];
                float next = snap ? target : Mathf.MoveTowards(current, target, Time.deltaTime * 1.85f);
                if (!snap && next == current)
                    continue;

                houseDoorOpening[i] = next;
                houseHinges[i].localRotation = Quaternion.Euler(0, -98f * Mathf.SmoothStep(0, 1, next), 0);
            }
        }

        internal void Animate()
        {
            if (wheel == null)
                return;
            wheel.Rotate(Vector3.forward, Time.deltaTime * 7f, Space.Self);
            foreach (Transform gondola in gondolas)
                gondola.rotation = Quaternion.identity;
        }

        private void DressBoulevard()
        {
            Color paving = new(0.72f, 0.69f, 0.63f);
            Color white = new(0.78f, 0.76f, 0.68f);
            foreach (float cross in new[] { 29f, 76f, 123f })
            {
                for (int stripe = -4; stripe <= 4; stripe++)
                {
                    art.BatchDecoration(new Vector3(-30 + stripe * 0.82f, 0.022f, cross + 4.8f),
                        new Vector3(0.45f, 0.03f, 2.7f), white);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int block = 0; block < 3; block++)
                {
                    float z = 52.5f + block * 47;
                    float x = -30 + side * 6;
                    GameObject sidewalk = art.Box("Boulevard sidewalk", new Vector3(x, .05f, z), new(2.7f, .10f, 32), paving,
                        false, "Concrete", new Vector2(2, 14));
                    art.MarkFootFloor(sidewalk, Floor.Stone);
                    art.Box("Road curb", new Vector3(x - side * 1.30f, 0.065f, z), new Vector3(0.12f, 0.13f, 32),
                        new Color(0.47f, 0.48f, 0.44f));
                    for (int planter = -1; planter <= 1; planter += 2)
                    {
                        Vector3 location = new(x + side * 0.31f, 0, z + planter * 10);
                        art.Box("Boulevard planter", location + Vector3.up * 0.17f, new Vector3(1.2f, 0.34f, 1.2f), Pavement);
                        GameObject soil = art.Box("Planter soil", location + Vector3.up * 0.345f,
                            new Vector3(1.03f, 0.025f, 1.03f), new Color(.20f, .19f, .15f), true, bake: false);
                        art.Damage.Mark(soil, CostaWorldDamage.Surface.Earth, 0);
                        Tree(location + Vector3.up * 0.345f);
                    }
                }
            }
        }

        private void Ground(float x, float z, float width, float depth)
        {
            art.Box("City ground", new Vector3(x, -0.25f, z), new Vector3(width, 0.50f, depth),
                new Color(0.59f, 0.61f, 0.61f), true, "Asphalt", new Vector2(width / 2.3f, depth / 2.3f));
        }

        private void Barrier(Vector3 position, Vector3 size)
        {
            art.Box("Perimeter seawall", position, size, Pavement, true, "Concrete",
                new Vector2(Mathf.Max(size.x, size.z) / 3f, 1.2f));
        }

        private static bool NearRoad(float position, float[] crossings)
        {
            foreach (float crossing in crossings)
                if (Mathf.Abs(position - crossing) < 3.9f)
                    return true;
            return false;
        }

        private void Mark(Vector3 position, Vector3 size)
        {
            art.BatchDecoration(position, size, new Color(0.55f, 0.51f, 0.41f));
        }

        private void Venue(string name, Vector3 entry, float width, float depth, float height, Color color, Color trim, Floor floor = Floor.Stone)
        {
            float front = entry.z - 2f;
            Footprints.Add(new Rect(entry.x - width * 0.5f, front, width, depth));
            Vector3 center = new(entry.x, 0, front + depth / 2);
            art.Acoustics.AddRoom(new Bounds(center + Vector3.up * 1.65f, new Vector3(width - .42f, 3.3f, depth)));
            float wing = (width - 2.8f) / 2;
            art.Wear(1, new Vector3(entry.x + width * 0.32f, 0.073f, front - 1.15f),
                new Vector2(2.8f, 1.7f), Quaternion.Euler(90, 0, 0));
            art.MarkFootFloor(art.Box("Entrance paving", new(entry.x, .035f, front - 1.4f),
                new(width + 2.5f, .07f, 3.2f), Pavement, false, "Concrete", new Vector2(width / 2, 2)), Floor.Stone);
            Color floorColor = floor == Floor.Wood ? new(.44f,.30f,.21f) : new(.70f,.67f,.59f);
            art.MarkFootFloor(art.Box("Lobby floor", center + Vector3.up * .016f, new(width - .6f,.03f,depth - .6f),
                floorColor, false, floor == Floor.Wood ? null : "Concrete", new Vector2(width / 3, depth / 3)), floor);

            for (int side = -1; side <= 1; side += 2)
            {
                art.Box("Lobby side", center + new Vector3(side * (width / 2 - 0.21f), 1.65f, 0),
                    new Vector3(0.42f, 3.30f, depth), color, true, "Concrete", new Vector2(depth / 2.4f, 2));
                float wallX = entry.x + side * (1.4f + wing / 2);
                art.Box("Stone window base", new Vector3(wallX, .47f, front),
                    new Vector3(wing, .94f, .44f), color, true, "Concrete", new Vector2(wing / 2, 1));
                art.Box("Stone window lintel", new Vector3(wallX, 2.85f, front),
                    new Vector3(wing, .90f, .44f), color, true, "Concrete", new Vector2(wing / 2, 1));
                for (int jamb = -1; jamb <= 1; jamb += 2)
                    art.Box("Stone window jamb", new Vector3(wallX + jamb * wing * .44f, 1.67f, front),
                        new Vector3(wing * .12f, 1.46f, .44f), color, true, "Concrete", Vector2.one);
                GameObject pane = art.Box("Lobby glazing",
                    new Vector3(wallX, 1.67f, front - 0.12f),
                    new Vector3(wing * 0.76f + .03f, 1.47f, 0.055f), Glass, true, bake: false, finish: HarborBlockout.Finish.Glass);
                art.Damage.Mark(pane, CostaWorldDamage.Surface.Glass, 10);
                art.Box("Metal entry column", new Vector3(entry.x + side * 1.53f, 1.58f, front - 0.22f),
                    new Vector3(0.18f, 3.16f, 0.18f), trim, finish: HarborBlockout.Finish.Metal);
                float bay = wallX;
                Color frame = Color.Lerp(Glass, trim, 0.33f);
                for (int mullion = -1; mullion <= 1; mullion++)
                    art.BatchDecoration(new Vector3(bay + mullion * wing * 0.23f, 1.67f, front - 0.273f),
                        new Vector3(0.065f, 1.45f, 0.048f), frame);
                art.BatchDecoration(new Vector3(bay, 0.91f, front - 0.29f),
                    new Vector3(wing * 0.78f, 0.08f, 0.16f), trim);

            }

            art.Box("Lobby back", center + new Vector3(0, 1.65f, depth / 2 - 0.21f),
                new Vector3(width, 3.3f, 0.42f), color, true, "Concrete", new Vector2(width / 2.4f, 2));
            art.Box(name, center + new Vector3(0, (3.3f + height) / 2, 0),
                new Vector3(width, height - 3.3f, depth), color, true, "Concrete", new Vector2(width / 2.4f, height / 2));
            art.Box("Entrance canopy", new Vector3(entry.x, 3.11f, front - 0.68f), new Vector3(5.2f, 0.17f, 1.85f), trim);
            art.Box("Facade sign panel", new Vector3(entry.x, 3.68f, front - 0.252f),
                new Vector3(Mathf.Min(width - 1, 10), 0.70f, 0.12f), trim);
            art.Sign(name, new Vector3(entry.x, 3.68f, front - 0.319f), 0.16f, Stone);

            GameObject fixture = new GameObject("Lobby lighting");
            fixture.transform.SetParent(root, false);
            fixture.transform.position = center + Vector3.up * 2.91f;
            Light lamp = fixture.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = Mathf.Max(7f, depth * 0.75f);
            lamp.intensity = 2.4f;
            lamp.color = new Color(1f, 0.87f, 0.72f);
            lamp.shadows = LightShadows.None;

            if (height > 8f)
                Windows(center, width, depth, height);
        }

        private void Block(Vector3 center, float width, float depth, float height)
        {
            Footprints.Add(new Rect(center.x - width * 0.5f, center.z - depth * 0.5f, width, depth));
            art.Box("Commercial block", center + Vector3.up * (height / 2), new Vector3(width, height, depth),
                Stone, true, "Concrete", new Vector2(width / 2.8f, height / 2.1f));
            Windows(center, width, depth, height);
        }

        private void Windows(Vector3 center, float width, float depth, float height)
        {
            for (float y = 5.35f; y < height - 0.75f; y += 2.65f)
            {
                for (float x = -width / 2 + 2; x < width / 2 - 0.7f; x += 2.75f)
                {
                    float facade = center.z - depth / 2;
                    art.BatchDecoration(new Vector3(center.x + x, y, facade - 0.024f),
                        new Vector3(1.54f, 1.32f, 0.049f), Glass, finish: HarborBlockout.Finish.Glass);
                    art.BatchDecoration(new Vector3(center.x + x, y - 0.65f, facade - 0.09f),
                        new Vector3(1.68f, 0.11f, 0.23f), Pavement);
                }
            }
        }

        private void BuildPark()
        {
            art.MarkFootFloor(art.Box("Park promenade", new(47,.023f,118), new(36,.045f,53), Pavement), Floor.Stone);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = 47 + side * 15;
                art.MarkFootFloor(art.Box("Park soil bed", new(x,.031f,118), new(4.6f,.052f,46),
                    new Color(.34f,.29f,.20f)), Floor.Earth);
            }
            art.Box("Entry canopy", ParkGate + new Vector3(0, 3.4f, 0), new Vector3(11, 0.30f, 1.9f), Brass);
            art.Sign("PARQUE MIRANTE", ParkGate + new Vector3(0, 3.29f, -1f), 0.18f, Stone);
            for (int side = -1; side <= 1; side += 2)
            {
                art.Box("Park entry post", ParkGate + new Vector3(side * 5.2f, 1.7f, 0),
                    new Vector3(0.4f, 3.4f, 0.5f), Brass, true);
                for (int i = 0; i < 5; i++)
                    Tree(new Vector3(47 + side * 16, 0, 98 + i * 8.6f));
                for (int i = 0; i < 3; i++)
                    Lamp(new Vector3(47 + side * 13, 0.04f, 97 + i * 19), Vector3.left * side);
            }

            wheel = new GameObject("Ferris wheel").transform;
            wheel.SetParent(root, false);
            wheel.position = ParkWheel + Vector3.up * 8.9f;
            Color steel = new(0.39f, 0.42f, 0.42f);
            for (int i = 0; i < 16; i++)
            {
                float t = i * Mathf.PI * 2 / 16;
                float next = (i + 1) * Mathf.PI * 2 / 16;
                Vector3 point = new(Mathf.Cos(t) * 7, Mathf.Sin(t) * 7, 0);
                Vector3 nextPoint = new(Mathf.Cos(next) * 7, Mathf.Sin(next) * 7, 0);
                Rail(wheel, point, nextPoint, 0.16f, Brass);
                Rail(wheel, Vector3.zero, point, 0.077f, steel);
                if (i % 2 == 0)
                {
                    gondolas[i / 2] = art.Part(wheel, PrimitiveType.Cube, "Gondola", point + Vector3.back * 0.47f,
                        new Vector3(1.16f, 0.73f, 0.75f), i % 4 == 0 ? Glass : Brass).transform;
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Rail(root, ParkWheel + new Vector3(side * 4.5f, 0.3f, 1.2f),
                    ParkWheel + new Vector3(0, 8.9f, 0.4f), 0.35f, steel);
                art.Box("Wheel footing", ParkWheel + new Vector3(side * 4.5f, 0.37f, 1.2f),
                    new Vector3(1.4f, 0.74f, 1.3f), Stone, true);
            }

            Fountain(new Vector3(42, 0, 101));
            Venue("BILHETERIA", new Vector3(35, 0, 133), 8, 8, 4.3f, Stone, Brass);
        }

        private void Desk(Vector3 point, Color color)
        {
            art.Box("Service counter", point + Vector3.up * 0.48f, new Vector3(2.6f, 0.96f, 0.62f), color, true);
            art.Box("Countertop", point + Vector3.up * 0.98f, new Vector3(2.74f, 0.10f, 0.74f), Stone);
        }

        private void Lamp(Vector3 point, Vector3 arm)
        {
            Shape("Street lamp", PrimitiveType.Cylinder, point + Vector3.up * 2.35f,
                new Vector3(0.13f, 2.35f, 0.13f), Glass);
            bool alongX = arm.x != 0;
            art.BatchDecoration(point + Vector3.up * 4.66f + arm * 0.55f,
                alongX ? new Vector3(1.1f, 0.16f, 0.39f) : new Vector3(0.39f, 0.16f, 1.1f), Glass);
            art.StreetLampDiffuser(point + Vector3.up * 4.574f + arm * 0.67f,
                alongX ? new Vector3(0.68f, 0.034f, 0.26f) : new Vector3(0.26f, 0.034f, 0.68f));
        }

        private void Tree(Vector3 point)
        {
            var tree = new GameObject("Street tree").transform;
            tree.SetParent(root, false);
            tree.localPosition = point;
            GameObject trunk = art.Part(tree, PrimitiveType.Cylinder, "Tree trunk", Vector3.up * 1.6f,
                new Vector3(.25f, 1.6f, .25f), new Color(.30f,.24f,.17f), true);
            art.Damage.Mark(trunk, CostaWorldDamage.Surface.Wood, 0);
            Color leaf = new(.26f,.36f,.26f);
            GameObject crown = art.Part(tree, PrimitiveType.Sphere, "Tree crown", Vector3.up * 3.7f,
                new Vector3(3,3,2.65f), leaf);
            GameObject offset = art.Part(tree, PrimitiveType.Sphere, "Tree crown", new Vector3(.70f,3.2f,.21f),
                new Vector3(2.5f,2.08f,2.16f), new Color(.31f,.41f,.29f));
            art.TreeDetail(tree, crown.GetComponent<Renderer>(),
                offset.GetComponent<Renderer>(), new Vector3(.31f,3.58f,.07f), leaf);
        }

        private void Fountain(Vector3 point)
        {
            Shape("Fountain basin", PrimitiveType.Cylinder, point + Vector3.up * 0.27f, new Vector3(6.7f, 0.27f, 6.7f), Stone);
            Shape("Fountain water", PrimitiveType.Cylinder, point + Vector3.up * 0.53f, new Vector3(6.16f, 0.025f, 6.16f), Glass);
            Shape("Fountain column", PrimitiveType.Cylinder, point + Vector3.up * 0.85f, new Vector3(0.78f, 0.54f, 0.78f), Stone);
        }

        private void Rail(Transform parent, Vector3 a, Vector3 b, float width, Color color)
        {
            Transform span = art.Part(parent, PrimitiveType.Cylinder, "Steel brace", (a + b) / 2,
                new Vector3(width, Vector3.Distance(a, b) / 2, width), color).transform;
            span.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        }

        private void Shape(string name, PrimitiveType primitive, Vector3 center, Vector3 size, Color color)
        {
            GameObject part = art.Part(root, primitive, name, center, size, color,
                name == "Tree trunk" || name == "Fountain basin" || name == "Fountain column");
            if (name == "Tree trunk")
                art.Damage.Mark(part, CostaWorldDamage.Surface.Wood, 0);
            if (name == "Tree crown" || name == "Street lamp")
                part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
