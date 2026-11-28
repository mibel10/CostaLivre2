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
            new("Calado City", 265, new Color(0.20f, 0.62f, 0.64f)),
            new("Antutu City", 495, new Color(0.26f, 0.58f, 0.78f)),
            new("Vice City", 721, new Color(0.92f, 0.40f, 0.60f))
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

        private static readonly Color Pavement = new(0.84f, 0.79f, 0.70f);
        private static readonly Color Stone = new(0.91f, 0.86f, 0.76f);
        private static readonly Color Glass = new(0.14f, 0.36f, 0.44f);
        private static readonly Color LinenSign = new(0.95f, 0.93f, 0.86f);
        private static readonly Color Brass = new(0.82f, 0.60f, 0.28f);
        private static readonly Color Turquoise = new(0.10f, 0.66f, 0.70f);
        private static readonly Color Terracotta = new(0.82f, 0.44f, 0.33f);
        private static readonly Color NeonPink = new(0.96f, 0.36f, 0.62f);
        private static readonly Color NeonCyan = new(0.18f, 0.84f, 0.86f);
        private static readonly Color NeonGold = new(0.98f, 0.74f, 0.28f);

        private static readonly Color[] SantaLuzPalette =
        {
            new(0.88f, 0.60f, 0.48f), // Coral colonial
            new(0.91f, 0.79f, 0.49f), // Amarelo sol
            new(0.58f, 0.80f, 0.74f), // Verde-água tropical
            new(0.58f, 0.75f, 0.86f), // Azul Bahia
            new(0.92f, 0.87f, 0.77f), // Creme areia
            new(0.90f, 0.68f, 0.58f)  // Pêssego praiano
        };

        private static readonly Color[] CaladoPalette =
        {
            new(0.86f, 0.56f, 0.38f), // Terracota portuária
            new(0.88f, 0.74f, 0.40f), // Ocre quente
            new(0.42f, 0.68f, 0.66f), // Turquesa cais
            new(0.46f, 0.62f, 0.78f), // Azul marítimo
            new(0.88f, 0.81f, 0.68f)  // Reboco solar
        };

        private static readonly Color[] AntutuPalette =
        {
            new(0.89f, 0.91f, 0.90f), // Branco costeiro moderno
            new(0.66f, 0.78f, 0.84f), // Vidro-azul claro
            new(0.85f, 0.80f, 0.71f), // Arenito sofisticado
            new(0.58f, 0.76f, 0.76f)  // Verde-mar executivo
        };

        private static readonly Color[] VicePalette =
        {
            new(0.92f, 0.60f, 0.70f), // Rosa flamingo Art Déco
            new(0.52f, 0.83f, 0.79f), // Menta Vice
            new(0.94f, 0.71f, 0.54f), // Pêssego pôr-do-sol
            new(0.94f, 0.90f, 0.82f), // Creme Art Déco
            new(0.76f, 0.66f, 0.84f)  // Lavanda tropical
        };

        private static readonly Color[] AwningPalette =
        {
            new(0.84f, 0.34f, 0.28f),
            new(0.14f, 0.58f, 0.62f),
            new(0.88f, 0.64f, 0.22f),
            new(0.20f, 0.56f, 0.38f),
            new(0.84f, 0.34f, 0.54f)
        };

        private static readonly string[] StoreSigns =
        {
            "AÇAÍ & SUCOS", "PADARIA SOL", "BAR DO PORTO", "SURF & MAR",
            "CAFÉ BAHIA", "SORVETERIA", "MODA PRAIA", "MERCADINHO",
            "BRISA LOUNGE", "TROPICAL CLUB", "GALERIA LUZ", "CHURRASCARIA"
        };
        private readonly HarborBlockout art;
        private readonly Transform root;
        private Transform wheel;
        internal CostaInteriors Interiors { get; }
        private float wheelAngle;
        private readonly Transform[] gondolas = new Transform[8];

        internal readonly List<Rect> Footprints = new();
        internal readonly GameObject[] HouseDoors = new GameObject[Estate.Length];
        private readonly Transform[] houseHinges = new Transform[Estate.Length];
        private readonly float[] houseDoorOpening = new float[Estate.Length];

        internal CostaCity(HarborBlockout art, Transform root)
        {
            this.art = art;
            this.root = root;
            Interiors = new CostaInteriors(art, root);
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

            Venue("GRAND HOTEL ATLÂNTICO", Hotel, 25, 16, 23, new Color(0.94f, 0.89f, 0.79f), new Color(0.86f, 0.44f, 0.38f));
            art.NeonTrim(Hotel + new Vector3(0, 22.7f, -2.05f), new Vector3(24.4f, 0.26f, 0.16f), NeonCyan);
            art.NeonTrim(Hotel + new Vector3(0, 3.98f, -2.28f), new Vector3(11.0f, 0.10f, 0.10f), NeonPink);
            Venue("JOALHERIA ÁUREA", Jeweler, 18, 14, 6, new Color(0.91f, 0.81f, 0.62f), new Color(0.18f, 0.56f, 0.54f));
            Venue("MUSEU DE SANTA LUZ", Museum, 27, 17, 7, new Color(0.92f, 0.86f, 0.76f), new Color(0.24f, 0.50f, 0.68f));
            Venue("POLÍCIA • SANTA LUZ", Station, 23, 16, 8, new Color(0.86f, 0.90f, 0.92f),
                new Color(0.18f, 0.38f, 0.60f));

            Venue("ARMARIA SANTA LUZ", GunShop, 12, 11, 6.8f, new Color(0.88f, 0.72f, 0.48f), new Color(0.74f, 0.34f, 0.28f));
            Venue("SANTA LUZ INFORMÁTICA", Electronics, 19, 16, 10.5f, new Color(0.76f, 0.86f, 0.88f),
                new Color(0.14f, 0.58f, 0.64f));
            art.NeonTrim(Electronics + new Vector3(0, 10.2f, -2.05f), new Vector3(18.2f, 0.20f, 0.14f), NeonCyan);
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
                Color villaColor = i == 0 ? new Color(0.90f, 0.66f, 0.55f) : i == 1 ? new Color(0.92f, 0.83f, 0.56f) : new Color(0.64f, 0.83f, 0.78f);
                Venue(address, Estate[i], 16, 11, 4.3f, villaColor, Terracotta, Floor.Wood);
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
            Venue("HOSPITAL COSTEIRO", CostaInteriorPlan.Hospital.Entry, 23, 17, 10, Stone,
                CostaInteriorPlan.Hospital.Accent);
            Block(new Vector3(32, 0, 151), 21, 12, 24);
            Block(new Vector3(-137, 0, 121), 13, 16, 13);
            Block(new Vector3(-55, 0, 118), 17, 7, 10);
            Venue("CAFÉ DA PRAÇA", CostaInteriorPlan.Cafe, 12, 7, 5.3f, new Color(0.91f, 0.74f, 0.56f), Terracotta);
            for (int i = 0; i < 4; i++)
                Tree(new Vector3(-18 + i * 8, 0, 38));
            Block(new Vector3(-140, 0, 65), 14, 19, 12);
            Block(new Vector3(-12, 0, 139), 22, 18, 17);
            Block(new Vector3(5, 0, -43), 16, 14, 8);
            for (int i = 0; i < 9; i++)
                Tree(new Vector3(-142 + 7.5f * i, 0, 32));
            // Coastal waterfront palms along the harbor edge
            for (int i = 0; i < 5; i++)
                Tree(new Vector3(17f, 0, 26f + i * 11f));
            Fountain(new Vector3(-56, 0, 39));
            yield return 0.58f;

            BuildHighway();
            for (int i = 0; i < Districts.Length; i++)
            {
                BuildDistrict(Districts[i], i);
                yield return 0.65f + 0.10f * i;
            }

            foreach (float part in Interiors.BuildStructure())
                yield return Mathf.Lerp(.86f,.93f,part);
            foreach (float part in art.FinishDecoration())
                yield return Mathf.Lerp(.93f, .99f, part);
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
                {
                    Lamp(new Vector3(HighwayX + side * 10.5f, 0, z), Vector3.left * side);
                    if (z < HighwayEnd - 14f)
                        Palm(new Vector3(HighwayX + side * 11.4f, 0, z + 14f));
                }
            art.Sign("RODOVIA COSTEIRA", new Vector3(HighwayX, 4.1f, HighwayStart + 5), 0.28f, LinenSign);
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
                    if (row == -1 && index == 2 && block == 4)
                        Venue("APARTAMENTOS MIRANTE", CostaInteriorPlan.Apartment.Entry, 11, 17, 10,
                            Stone, CostaInteriorPlan.Apartment.Accent);
                    else if (row == -1 && index == 0 && block == 5)
                        Venue("GARAGEM DO CAIS", CostaInteriorPlan.Garage.Entry, 11, 19, 7,
                            Stone, CostaInteriorPlan.Garage.Accent);
                    else
                        Block(new Vector3(x, 0, z + row * 37), 11, depth, height, index);
                    Tree(new Vector3(x + 5, 0, z + row * 13));
                }

            switch (index)
            {
                case 0:
                    Venue("MERCADO DO CAIS", new Vector3(-69, 0, z + 9), 23, 16, 8, new Color(0.90f, 0.77f, 0.50f), district.Accent);
                    art.Mural(1, new Vector3(-69f, 2.3f, z + 16.95f), new Vector2(5.4f, 2.5f), 180f);
                    for (int i = 0; i < 3; i++)
                    {
                        Color stallTint = AwningPalette[i % AwningPalette.Length];
                        art.Box("Market stall", new Vector3(9 + i * 9, 0.55f, z - 13), new Vector3(3.8f, 1.1f, 2),
                            stallTint, true);
                        art.BatchDecoration(new Vector3(9 + i * 9, 2.35f, z - 13), new Vector3(4.1f, 0.14f, 2.3f),
                            i % 2 == 0 ? LinenSign : stallTint);
                    }
                    break;
                case 1:
                    Venue("CENTRO TECNOLÓGICO", CostaInteriorPlan.Office.Entry, 28, 17, 17, new Color(0.84f, 0.90f, 0.92f), district.Accent);
                    art.NeonTrim(CostaInteriorPlan.Office.Entry + new Vector3(0, 16.6f, -2.06f), new Vector3(27.2f, 0.24f, 0.14f), NeonCyan);
                    Plaza(new Vector3(12, 0, z - 15), district.Accent);
                    break;
                default:
                    Venue("CLUBE DO MIRANTE", new Vector3(-72, 0, z + 9), 24, 17, 10, new Color(0.93f, 0.67f, 0.74f), NeonPink);
                    art.NeonTrim(new Vector3(-72f, 9.75f, z + 6.94f), new Vector3(23.4f, 0.24f, 0.14f), NeonPink);
                    art.NeonTrim(new Vector3(-72f, 4.18f, z + 6.72f), new Vector3(10.6f, 0.12f, 0.10f), NeonCyan);
                    art.Mural(2, new Vector3(-72f, 2.5f, z + 24.06f), new Vector2(5.8f, 2.6f), 180f);
                    Plaza(new Vector3(12, 0, z - 15), NeonCyan);
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

        internal void Animate(Vector3 observer, float drawDistance)
        {
            if (wheel == null)
                return;
            wheelAngle = Mathf.Repeat(wheelAngle + Time.deltaTime * 7f, 360f);
            // Reflections can still see the wheel from a nearby capture outside the camera frustum.
            float visibleRange = Mathf.Max(200f, drawDistance + 16f);
            if ((observer - wheel.position).sqrMagnitude > visibleRange * visibleRange)
                return;
            wheel.localRotation = Quaternion.AngleAxis(wheelAngle, Vector3.forward);
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
                new Color(0.32f, 0.33f, 0.35f), true, "Asphalt", new Vector2(width / 2.3f, depth / 2.3f));
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
            art.BatchDecoration(position, size, new Color(0.90f, 0.85f, 0.72f));
        }

        private void Venue(string name, Vector3 entry, float width, float depth, float height, Color color, Color trim, Floor floor = Floor.Stone)
        {
            CostaInteriorPlan plan = CostaInteriorPlan.Find(entry);
            if (plan != null)
                height = Mathf.Max(4.3f, plan.Height);
            float front = entry.z - 2f;
            Footprints.Add(new Rect(entry.x - width * 0.5f, front, width, depth));
            Vector3 center = new(entry.x, 0, front + depth / 2);
            if (plan == null)
                art.Acoustics.AddRoom(new Bounds(center + Vector3.up * 1.65f, new Vector3(width - .42f, 3.3f, depth)));
            float opening = plan?.DoorSpan ?? 2.8f;
            float wing = (width - opening) / 2;
            art.Wear(1, new Vector3(entry.x + width * 0.32f, 0.073f, front - 1.15f),
                new Vector2(2.8f, 1.7f), Quaternion.Euler(90, 0, 0));
            art.MarkFootFloor(art.Box("Entrance paving", new(entry.x, .040f, front - 1.4f),
                new(width + 2.5f, .07f, 3.2f), Pavement, false, "Concrete", new Vector2(width / 2, 2)), Floor.Stone);
            Color floorColor = floor == Floor.Wood ? new(.44f,.30f,.21f) : new(.70f,.67f,.59f);
            art.MarkFootFloor(art.Box("Lobby floor", center + Vector3.up * .024f, new(width - .6f,.03f,depth - .6f),
                floorColor, false, floor == Floor.Wood ? null : "Concrete", new Vector2(width / 3, depth / 3)), floor);

            for (int side = -1; side <= 1; side += 2)
            {
                art.Box("Lobby side", center + new Vector3(side * (width / 2 - 0.21f), 1.65f, 0),
                    new Vector3(0.42f, 3.30f, depth), color, true, "Concrete", new Vector2(depth / 2.4f, 2));
                float wallX = entry.x + side * (opening / 2 + wing / 2);
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
                new Vector3(width - 0.84f, 3.3f, 0.42f), color, true, "Concrete", new Vector2(width / 2.4f, 2));
            if (plan == null || !plan.Vertical)
                art.Box(name, center + new Vector3(0, (3.306f + height) / 2, 0),
                    new Vector3(width, height - 3.306f, depth), color, true, "Concrete", new Vector2(width / 2.4f, height / 2));
            else
            {
                float facadeHeight = height - 3.3f;
                float middle = 3.3f + facadeHeight / 2;
                for (int side = -1; side <= 1; side += 2)
                {
                    art.Box("Upper side wall", new Vector3(entry.x + side * (width/2 - .21f), middle, center.z),
                        new(.42f, facadeHeight, depth), color, true, "Concrete");
                }
                art.Box("Upper rear facade", new Vector3(entry.x,middle,center.z+depth/2-.21f),
                    new(width-.84f,facadeHeight,.42f),color,true,"Concrete");
                int bays = Mathf.Max(2,Mathf.FloorToInt((width-.84f)/3.4f));
                float bayWidth = (width-.84f)/bays;
                for (int floorIndex=1; floorIndex<plan.Levels; floorIndex++)
                {
                    float y = plan.Y(floorIndex);
                    art.Box("Facade sill wall",new(entry.x,y+.42f,front+.21f),new(width-.84f,.88f,.42f),color,true,"Concrete");
                    art.Box("Facade lintel",new(entry.x,y+2.79f,front+.21f),new(width-.84f,1.02f,.42f),color,true,"Concrete");
                    for (int bayIndex=0; bayIndex<bays; bayIndex++)
                    {
                        float x = entry.x-width/2+.42f + (bayIndex+.5f)*bayWidth;
                        art.Box("Upper facade mullion",new(x-bayWidth/2+.19f,y+1.57f,front+.21f),
                            new(.38f,1.48f,.42f),color,true,"Concrete");
                        art.Box("Upper window glazing",new(x+.19f,y+1.57f,front+.14f),
                            new(bayWidth-.38f,1.48f,.08f),Glass,true,finish:HarborBlockout.Finish.Glass);
                    }
                }
            }
            art.Box("Entrance canopy", new Vector3(entry.x, 3.11f, front - 0.68f), new Vector3(5.2f, 0.17f, 1.85f), trim);
            art.Box("Facade sign panel", new Vector3(entry.x, 3.68f, front - 0.252f),
                new Vector3(Mathf.Min(width - 1, 10), 0.70f, 0.12f), trim);
            art.Sign(name, new Vector3(entry.x, 3.68f, front - 0.319f), 0.16f, Stone);

            if (plan != null && plan.Opens != plan.Closes)
                art.Sign($"{plan.Opens:00}:00 – {plan.Closes:00}:00",
                    new(entry.x + opening/2 + .4f,1.57f,front-.29f),.085f,LinenSign);

            if (plan == null)
            {
                GameObject fixture = new GameObject("Lobby lighting");
                fixture.transform.SetParent(root, false);
                fixture.transform.position = center + Vector3.up * 2.91f;
                Light lamp = fixture.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.range = Mathf.Max(7f, depth * 0.75f);
                lamp.intensity = 2.4f;
                lamp.color = new Color(1f, 0.87f, 0.72f);
                lamp.shadows = LightShadows.None;
            }

            if (height > 8f && (plan == null || !plan.Vertical))
                Windows(center, width, depth, height);
        }

        private static int BlockHash(Vector3 center) =>
            Mathf.Abs(Mathf.RoundToInt(center.x) * 73856093 ^ Mathf.RoundToInt(center.z) * 19349663);

        private void Block(Vector3 center, float width, float depth, float height, int district = -1)
        {
            Footprints.Add(new Rect(center.x - width * 0.5f, center.z - depth * 0.5f, width, depth));
            int hash = BlockHash(center);
            Color[] palette = district switch
            {
                0 => CaladoPalette,
                1 => AntutuPalette,
                2 => VicePalette,
                _ => SantaLuzPalette
            };
            Color facadeColor = palette[hash % palette.Length];
            Color trimColor = district == 2
                ? (hash % 2 == 0 ? NeonPink : NeonCyan)
                : district == 1
                    ? Turquoise
                    : AwningPalette[(hash / 3) % AwningPalette.Length];

            art.Box("Commercial block", center + Vector3.up * (height / 2), new Vector3(width, height, depth),
                facadeColor, true, "Concrete", new Vector2(width / 2.8f, height / 2.1f));

            float frontZ = center.z - depth * 0.5f;
            // Roof parapet cornice for crisp coastal silhouette
            art.BatchDecoration(new Vector3(center.x, height + 0.14f, center.z),
                new Vector3(width + 0.32f, 0.28f, depth + 0.32f),
                district == 1 ? new Color(0.24f, 0.34f, 0.40f) : Pavement);

            // Ground-floor coastal awning (toldo) + storefront glazing
            float awningWidth = Mathf.Min(width - 1.2f, 6.4f);
            art.BatchDecoration(new Vector3(center.x, 3.05f, frontZ - 0.52f),
                new Vector3(awningWidth, 0.18f, 1.05f), trimColor);
            art.BatchDecoration(new Vector3(center.x, 1.55f, frontZ - 0.04f),
                new Vector3(awningWidth * 0.86f, 2.35f, 0.06f), Glass, finish: HarborBlockout.Finish.Glass);

            // Vice City & commercial neon roof/facade accents (zero realtime lights)
            if (district == 2)
            {
                Color glow = (hash % 3) switch { 0 => NeonPink, 1 => NeonCyan, _ => NeonGold };
                art.NeonTrim(new Vector3(center.x, height - 0.18f, frontZ - 0.08f), new Vector3(width - 0.4f, 0.16f, 0.12f), glow);
                art.NeonTrim(new Vector3(center.x - width * 0.46f, height * 0.55f, frontZ - 0.08f), new Vector3(0.14f, height * 0.55f, 0.12f), glow);
            }
            else if (district == 1 && hash % 2 == 0)
            {
                art.NeonTrim(new Vector3(center.x, height - 0.18f, frontZ - 0.08f), new Vector3(width - 0.6f, 0.14f, 0.10f), NeonCyan);
            }

            // Deterministic Brazilian storefront signs & street-art murals
            if (hash % 2 == 0)
            {
                string label = StoreSigns[(hash / 5) % StoreSigns.Length];
                art.BatchDecoration(new Vector3(center.x, 3.55f, frontZ - 0.10f),
                    new Vector3(Mathf.Min(width - 1f, 5.4f), 0.56f, 0.12f), new Color(0.14f, 0.18f, 0.22f));
                art.Sign(label, new Vector3(center.x, 3.55f, frontZ - 0.17f), 0.115f, LinenSign);
            }
            if (hash % 3 == 0)
            {
                int muralDesign = (hash / 7) % 4;
                bool rightWall = (hash & 1) == 0;
                float wallX = center.x + (rightWall ? width * 0.5f + 0.04f : -width * 0.5f - 0.04f);
                art.Mural(muralDesign, new Vector3(wallX, 2.25f, center.z), new Vector2(Mathf.Min(depth - 2f, 5.2f), 2.5f),
                    rightWall ? -90f : 90f);
            }

            Windows(center, width, depth, height);
        }

        private void Windows(Vector3 center, float width, float depth, float height)
        {
            for (float y = 5.35f; y < height - 0.75f; y += 2.65f)
            {
                for (float x = -width / 2 + 2; x < width / 2 - 0.7f; x += 2.75f)
                {
                    float facade = center.z - depth / 2;
                    art.BatchDecoration(new Vector3(center.x + x, y, facade - 0.030f),
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
                art.MarkFootFloor(art.Box("Park grass bed", new(x,.031f,118), new(4.6f,.052f,46),
                    new Color(.22f,.52f,.28f)), Floor.Earth);
            }
            art.Box("Entry canopy", ParkGate + new Vector3(0, 3.4f, 0), new Vector3(11, 0.30f, 1.9f), Turquoise);
            art.NeonTrim(ParkGate + new Vector3(0, 3.58f, -0.96f), new Vector3(10.8f, 0.10f, 0.08f), NeonGold);
            art.Sign("PARQUE MIRANTE", ParkGate + new Vector3(0, 3.29f, -1f), 0.18f, LinenSign);
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
                    Color gondolaTint = AwningPalette[(i / 2) % AwningPalette.Length];
                    gondolas[i / 2] = art.Part(wheel, PrimitiveType.Cube, "Gondola", point + Vector3.back * 0.47f,
                        new Vector3(1.16f, 0.73f, 0.75f), gondolaTint).transform;
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
            int hash = BlockHash(point);
            if (hash % 3 != 0)
            {
                Palm(point);
                return;
            }
            var tree = new GameObject("Tropical street tree").transform;
            tree.SetParent(root, false);
            tree.localPosition = point;
            GameObject trunk = art.Part(tree, PrimitiveType.Cylinder, "Tree trunk", Vector3.up * 1.6f,
                new Vector3(.26f, 1.6f, .26f), new Color(.36f, .27f, .19f), true);
            art.Damage.Mark(trunk, CostaWorldDamage.Surface.Wood, 0);
            // Lush Brazilian foliage or flowering Ipê (yellow / pink)
            bool flowering = hash % 6 == 0;
            Color leaf = flowering
                ? (hash % 12 == 0 ? new Color(0.92f, 0.74f, 0.20f) : new Color(0.86f, 0.36f, 0.56f))
                : new Color(0.18f, 0.50f, 0.27f);
            Color highlight = flowering
                ? Color.Lerp(leaf, new Color(0.98f, 0.90f, 0.66f), 0.25f)
                : new Color(0.26f, 0.58f, 0.31f);
            GameObject crown = art.Part(tree, PrimitiveType.Sphere, "Tree crown", Vector3.up * 3.7f,
                new Vector3(3.15f, 2.95f, 2.85f), leaf);
            GameObject offset = art.Part(tree, PrimitiveType.Sphere, "Tree crown", new Vector3(.72f, 3.25f, .22f),
                new Vector3(2.55f, 2.15f, 2.25f), highlight);
            art.TreeDetail(tree, crown.GetComponent<Renderer>(),
                offset.GetComponent<Renderer>(), new Vector3(.31f, 3.58f, .07f), leaf);
        }

        private void Palm(Vector3 point)
        {
            int hash = BlockHash(point);
            float height = 4.6f + (hash % 3) * 0.75f;
            float leanYaw = (hash % 8) * 45f;
            var palm = new GameObject("Tropical palm").transform;
            palm.SetParent(root, false);
            palm.localPosition = point;
            palm.localRotation = Quaternion.Euler(0, leanYaw, 0);

            Color bark = new(0.45f, 0.35f, 0.24f);
            Color frondDark = new(0.16f, 0.48f, 0.24f);
            Color frondLight = new(0.24f, 0.58f, 0.29f);
            GameObject trunk = art.Part(palm, PrimitiveType.Cylinder, "Tree trunk", new Vector3(0.12f, height * 0.5f, 0f),
                new Vector3(0.22f, height * 0.5f, 0.22f), bark, true);
            trunk.transform.localRotation = Quaternion.Euler(0, 0, -4.5f);
            art.Damage.Mark(trunk, CostaWorldDamage.Surface.Wood, 0);

            Vector3 top = new(0.28f, height - 0.12f, 0f);
            var fronds = new Renderer[5];
            for (int i = 0; i < 5; i++)
            {
                float a = i * (Mathf.PI * 2f / 5f);
                Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                GameObject frond = art.Part(palm, PrimitiveType.Cube, "Palm frond",
                    top + dir * 1.15f + Vector3.up * 0.08f,
                    new Vector3(0.56f, 0.08f, 2.35f), i % 2 == 0 ? frondDark : frondLight);
                frond.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(20f, 0f, 0f);
                fronds[i] = frond.GetComponent<Renderer>();
            }
            art.TreeDetail(palm, fronds, top + Vector3.up * 0.15f, frondDark);
        }

        private void Fountain(Vector3 point)
        {
            Shape("Fountain basin", PrimitiveType.Cylinder, point + Vector3.up * 0.27f, new Vector3(6.7f, 0.27f, 6.7f), Stone);
            GameObject pool = art.Part(root, PrimitiveType.Cylinder, "Fountain water", point + Vector3.up * 0.53f,
                new Vector3(6.16f, 0.025f, 6.16f), Turquoise, false, HarborBlockout.Finish.Glass);
            pool.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
