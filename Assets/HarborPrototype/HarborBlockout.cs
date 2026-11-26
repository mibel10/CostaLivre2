using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace CostaLivre
{
    internal sealed class HarborBlockout
    {
        internal static readonly Vector3 Spawn = new(-11, 0, -14);
        internal static readonly Vector3 Cargo = new(17, 0, 12);
        internal static readonly Vector3 Safehouse = new(-20, 0, -11);
        internal static readonly Color Amber = new(0.94f, 0.67f, 0.43f);
        internal static readonly Color Mint = new(0.67f, 0.83f, 0.76f);

        internal static readonly Vector2[] WarehouseCenters = { new(-11, 8), new(8.5f, 9), new(-10.5f, -7.5f), new(8.5f, -9.5f) };

        private readonly Transform root;
        private readonly Shader shader;
        private readonly Material surfaceTemplate = Resources.Load<Material>("Harbor/HarborSurface");
        private readonly Dictionary<Color, Material> palette = new();
        private readonly Dictionary<(string, Color, Vector2), Material> surfaces = new();
        private readonly Dictionary<string, (Texture2D Color, Texture2D Normal)> maps = new();
        private readonly List<Mesh> models = new();
        private readonly Dictionary<(Vector2Int Cell, Material Material), List<MeshFilter>> decoration = new();
        private readonly List<NavMeshBuildSource> walkableGeometry = new();
        private readonly HarborCharacter.Art characterArt = new();
        private Font signFont;
        private Material signMaterial;

        internal HarborBlockout(Transform root, Shader shader)
        {
            this.root = root;
            this.shader = shader;
            foreach (string name in new[] { "Asphalt", "Concrete", "Steel", "Walker" })
                maps.Add(name, (Resources.Load<Texture2D>($"Harbor/{name}Color"), Resources.Load<Texture2D>($"Harbor/{name}Normal")));
        }

        internal void Build()
        {
            Color road = new(0.58f, 0.62f, 0.65f);
            Color concrete = new(0.70f, 0.72f, 0.70f);
            Color water = new(0.10f, 0.21f, 0.25f);
            Color paint = new(0.58f, 0.51f, 0.35f);

            Box("Bay", new(22, -0.65f, 0), new(100, 0.15f, 110), water);
            Box("Harbor streets", new(0, -0.25f, 0), new(50, 0.5f, 44), road, true, "Asphalt", new(21, 19));
            Box("Quay edge", new(23.7f, 0.04f, 0), new(2.6f, 0.08f, 44), concrete, false, "Concrete", new(3, 28));
            Box("Quay parapet", new(24.7f, 0.68f, 0), new(0.35f, 1.36f, 44), concrete, true, "Concrete", new(24, 1));

            for (int i = -4; i <= 4; i++)
            {
                if (i != 0)
                {
                    Box("Lane stripe", new(i * 5, 0.015f, 0), new(2.25f, 0.03f, 0.12f), paint);
                    Box("Lane stripe", new(0, 0.015f, i * 4.5f), new(0.12f, 0.03f, 2.1f), paint);
                }
                Part(root, PrimitiveType.Cylinder, "Mooring bollard", new(23.4f, 0.34f, i * 4.4f),
                    new(0.27f, 0.34f, 0.27f), new Color(0.20f, 0.20f, 0.18f));
            }

            Warehouse(0, "REDE & CABO", 6.0f, new Color(0.66f, 0.73f, 0.67f));
            Warehouse(1, "CÂMARA FRIA", 5.2f, new Color(0.77f, 0.80f, 0.84f));
            Warehouse(2, "OFICINA NAVAL", 4.1f, new Color(0.83f, 0.76f, 0.65f));
            Warehouse(3, "ARMAZÉM 04", 5.7f, new Color(0.78f, 0.69f, 0.59f));

            Container(new Vector3(20, 0, 5.5f), new Color(0.60f, 0.69f, 0.73f));
            Container(new Vector3(20, 0, -6.5f), new Color(0.76f, 0.60f, 0.51f));
            Container(new Vector3(13, 0, 17.5f), new Color(0.64f, 0.71f, 0.63f));
            Container(new Vector3(-17.5f, 0, 13.5f), new Color(0.75f, 0.66f, 0.53f));
            DressQuay();

            Pad("Cargo pickup", Cargo, Amber);
            Pad("Safehouse", Safehouse, Mint);
            Box("Safehouse wall", Safehouse + new Vector3(-2.1f, 1.2f, 0),
                new Vector3(0.5f, 2.4f, 4), new Color(0.62f, 0.74f, 0.71f), true, "Concrete", new(3, 2));
            Box("Safehouse sign", Safehouse + new Vector3(-1.82f, 1.9f, 0),
                new Vector3(0.07f, 0.25f, 2.3f), Mint);
        }

        internal AsyncOperation BuildNavigation(NavMeshData data)
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(0);
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.24f;
            return NavMeshBuilder.UpdateNavMeshDataAsync(data, settings, walkableGeometry,
                new Bounds(new Vector3(-37, 3, 361), new Vector3(238, 46, 864)));
        }

        internal HarborCharacter Person(string name, Transform parent, Vector3 position, Color tint)
        {
            return new HarborCharacter(characterArt, name, parent, position, Surface("Walker", tint, Vector2.one));
        }

        internal GameObject[] PoliceUniform(HarborCharacter officer)
        {
            Color navy = new(0.072f, 0.102f, 0.155f);
            Color armor = new(0.14f, 0.18f, 0.225f);
            Color badge = new(0.65f, 0.73f, 0.77f);
            Transform root = officer.Root;
            Transform helmet = Part(root, PrimitiveType.Sphere, "Police helmet", new(0, 1.68f, 0.015f),
                new(0.223f, 0.168f, 0.25f), navy).transform;
            helmet.SetParent(officer.Head, true);
            Transform brim = Part(root, PrimitiveType.Cube, "Police visor", new(0, 1.65f, 0.098f),
                new(0.207f, 0.032f, 0.142f), navy).transform;
            brim.SetParent(officer.Head, true);

            Transform vest = new GameObject("Uniform protective vest").transform;
            vest.SetParent(root, false);
            vest.localPosition = new Vector3(0, 1.27f, 0);
            Model(vest, "Front vest", armor, new(0.045f, 0.17f, -0.19f, 0.17f),
                new(0.14f, 0.169f, -0.174f, 0.155f), new(0.163f, 0.142f, -0.146f, 0.131f));
            Model(vest, "Back vest", armor, new(-0.155f, 0.144f, -0.158f, 0.137f),
                new(-0.132f, 0.172f, -0.176f, 0.160f), new(-0.040f, 0.155f, -0.178f, 0.163f));
            Part(vest, PrimitiveType.Cube, "Chest badge", new(0.09f, 0.083f, 0.166f), new(0.072f, 0.038f, 0.011f), badge);
            Part(vest, PrimitiveType.Cube, "Reflective back stripe", new(0, 0.058f, -0.158f),
                new(0.249f, 0.043f, 0.009f), badge);
            Part(vest, PrimitiveType.Cube, "Duty radio", new(-0.130f, 0.11f, 0.143f),
                new(0.063f, 0.12f, 0.055f), navy);
            vest.SetParent(officer.Chest, true);
            return new[] { helmet.gameObject, brim.gameObject, vest.gameObject };
        }

        internal GameObject Pack(Transform wearer, Transform chest)
        {
            Color canvas = new(0.21f, 0.23f, 0.19f);
            GameObject pack = new GameObject("Cargo backpack");
            pack.transform.SetParent(wearer, false);
            pack.transform.localPosition = new Vector3(0, 1.13f, -0.17f);
            pack.transform.SetParent(chest, true);
            Part(pack.transform, PrimitiveType.Capsule, "Canvas shell", new(0, 0.02f, -0.04f),
                new(0.32f, 0.25f, 0.21f), canvas);
            Part(pack.transform, PrimitiveType.Cube, "Front pocket", new(0, -0.072f, -0.14f),
                new(0.24f, 0.18f, 0.07f), new Color(0.24f, 0.27f, 0.22f));
            return pack;
        }

        internal GameObject Case()
        {
            GameObject cargo = Box("Cargo case", Cargo + Vector3.up * 0.39f, new(0.91f, 0.50f, 0.66f), Amber);
            Part(cargo.transform, PrimitiveType.Cube, "Case stripe", new(0, 0.05f, -0.51f),
                new(0.7f, 0.28f, 0.04f), new Color(0.17f, 0.22f, 0.25f));
            return cargo;
        }

        internal Transform WeaponModel(Transform model, int slot, float length, Color body)
        {
            Color metal = new(0.13f, 0.17f, 0.21f);
            float width = slot == 0 ? 0.028f : slot == 1 ? 0.042f : 0.048f;
            Model(model, "Contoured receiver", body,
                new(-0.075f, width * 0.70f, -0.056f, 0.048f),
                new(-0.027f, width, -0.066f, 0.063f),
                new(length * 0.23f, width, -0.055f, 0.062f),
                new(length * 0.50f, width * 0.84f, -0.046f, 0.045f),
                new(length * 0.58f, width * 0.68f, -0.031f, 0.032f));
            Transform grip = Model(model, "Angled grip", metal,
                new(-0.065f, 0.031f, -0.16f, -0.066f), new(-0.031f, 0.039f, -0.18f, -0.039f),
                new(0.031f, 0.036f, -0.163f, -0.037f), new(0.058f, 0.026f, -0.13f, -0.06f));
            grip.localRotation = Quaternion.Euler(13, 0, 0);
            Part(model, PrimitiveType.Cube, "Trigger guard", new(0, -0.085f, 0.097f),
                new(0.055f, 0.025f, 0.13f), metal);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int detail = 0; detail < 5; detail++)
                {
                    Part(model, PrimitiveType.Cube, "Receiver serration", new(side * width, 0.028f, 0.025f + detail * 0.016f),
                        new(0.006f, 0.04f, 0.006f), metal);
                }
            }
            Transform barrel = Part(model, PrimitiveType.Cylinder, "Barrel", new(0, 0.026f, length * 0.70f),
                new(slot == 1 ? 0.095f : 0.064f, length * 0.275f, 0.064f), metal).transform;
            barrel.localRotation = Quaternion.Euler(90, 0, 0);

            if (slot > 0)
            {
                Model(model, "Tapered stock", body, new(-0.34f, 0.039f, -0.081f, 0.056f),
                    new(-0.27f, 0.047f, -0.080f, 0.059f), new(-0.12f, 0.029f, -0.037f, 0.037f),
                    new(-0.04f, 0.040f, -0.037f, 0.041f));
                Part(model, PrimitiveType.Cube, "Pad", new(0, -0.04f, -0.33f),
                    new(0.12f, 0.18f, 0.05f), metal);
            }

            if (slot == 2 || slot == 3)
            {
                Part(model, PrimitiveType.Cube, "Magazine", new(0, -0.13f, length * 0.32f),
                    new(0.09f, 0.18f, 0.13f), metal);
            }

            if (slot == 4)
            {
                Part(model, PrimitiveType.Cube, "Scope mount", new(0, 0.09f, 0.20f),
                    new(0.08f, 0.12f, 0.15f), metal);
                Transform scope = Part(model, PrimitiveType.Cylinder, "Scope", new(0, 0.17f, 0.20f),
                    new(0.11f, 0.17f, 0.11f), metal).transform;
                scope.localRotation = Quaternion.Euler(90, 0, 0);
            }

            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0, 0.026f, length);
            return muzzle;
        }

        internal Transform WeaponFlash(Transform parent)
        {
            return Part(parent, PrimitiveType.Sphere, "Muzzle flash", Vector3.zero,
                new(0.115f, 0.115f, 0.21f), Amber).transform;
        }

        internal void ReleaseModel(Mesh mesh)
        {
            if (models.Remove(mesh))
                Object.Destroy(mesh);
        }

        internal void ReleaseMaterials()
        {
            if (signMaterial != null)
            {
                Font.textureRebuilt -= RefreshSignAtlas;
                Object.Destroy(signMaterial);
            }
            foreach (Material material in palette.Values)
                Object.Destroy(material);
            foreach (Material material in surfaces.Values)
                Object.Destroy(material);
            foreach (Mesh model in models)
                Object.Destroy(model);
        }

        private void Warehouse(int index, string sign, float height, Color color)
        {
            Vector2 center = WarehouseCenters[index];
            float x = center.x;
            float z = center.y;
            const float width = 10.5f;
            const float depth = 8.5f;
            float facade = z - depth / 2;
            Color trim = new(0.20f, 0.23f, 0.24f);
            Box(sign, new(x, height / 2, z), new(width, height, depth), color, true, "Concrete", new(5.5f, 3));
            Box("Flashing", new(x, height + 0.05f, z), new(width + 0.28f, 0.18f, depth + 0.25f), trim);
            Box("Foundation", new(x, 0.23f, facade - 0.04f), new(width, 0.46f, 0.11f),
                new Color(0.53f, 0.56f, 0.52f), false, "Concrete", new(6, 1));
            Box("Loading frame", new(x + 2, 1.4f, facade - 0.052f), new(2.96f, 2.8f, 0.15f), trim);
            Box("Roll-up shutter", new(x + 2, 1.36f, facade - 0.134f), new(2.68f, 2.58f, 0.05f),
                new Color(0.66f, 0.69f, 0.68f), false, "Steel", new(1, 2));
            Box("Sign backing", new(x - 1.85f, 2.60f, facade - 0.13f), new(4.27f, 0.55f, 0.12f), trim);
            Sign(sign, new(x - 1.85f, 2.60f, facade - 0.201f), 0.11f, new Color(0.79f, 0.77f, 0.67f));
            for (int window = -1; window <= 1; window++)
            {
                float offset = x + window * 2.7f;
                Box("Steel window frame", new(offset, height - 0.80f, facade - 0.08f),
                    new(1.86f, 0.64f, 0.16f), trim);
                for (int pane = -1; pane <= 1; pane++)
                {
                    Box("Frosted glass", new(offset + pane * 0.58f, height - 0.80f, facade - 0.168f),
                        new(0.52f, 0.46f, 0.025f), new Color(0.26f, 0.34f, 0.36f));
                }
            }
            for (int pillar = -1; pillar <= 1; pillar += 2)
            {
                Box("Corner reinforcement", new(x + pillar * 4.84f, height / 2, facade - 0.08f),
                    new(0.30f, height - 0.17f, 0.16f), color, false, "Concrete", new(1, 3));
            }
            Box("Roof ventilation", new(x - 2.1f, height + 0.42f, z + 0.8f),
                new(2, 0.66f, 1.55f), trim, false, "Steel", Vector2.one);
        }

        private void Container(Vector3 center, Color color)
        {
            Box("Shipping container", center + Vector3.up * 1.05f, new(2.2f, 2.1f, 4.1f),
                color, true, "Steel", new(3, 1.6f));
            Color rails = color * 0.48f;
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Container top rail", center + new Vector3(side * 1.1f, 2.05f, 0), new(0.1f, 0.13f, 4.14f), rails);
                Box("Container base rail", center + new Vector3(side * 1.1f, 0.10f, 0), new(0.1f, 0.12f, 4.14f), rails);
                for (int end = -1; end <= 1; end += 2)
                {
                    Box("Container corner", center + new Vector3(side * 1.06f, 1.05f, end * 2.05f),
                        new(0.12f, 2.08f, 0.11f), rails);
                }
                Box("Door locking bar", center + new Vector3(side * 0.7f, 1.03f, -2.14f),
                    new(0.035f, 1.8f, 0.035f), rails);
            }
        }

        private void DressQuay()
        {
            Color steel = new(0.24f, 0.27f, 0.26f);
            Color crane = new(0.57f, 0.37f, 0.20f);
            for (int i = -2; i <= 2; i++)
            {
                float z = 7.5f * i + 2;
                Box("Lamp column", new(23.3f, 2.7f, z), new(0.10f, 5.4f, 0.10f), steel);
                Box("Lamp arm", new(22.7f, 5.37f, z), new(1.29f, 0.12f, 0.10f), steel);
                Box("Lamp head", new(22.06f, 5.31f, z), new(0.34f, 0.11f, 0.22f), new Color(0.70f, 0.68f, 0.53f));
            }

            Box("Pier beyond the parapet", new(29.5f, -0.30f, 12), new(9.5f, 0.58f, 17),
                new Color(0.68f, 0.68f, 0.63f), false, "Concrete", new(6, 10));
            for (int side = -1; side <= 1; side += 2)
            {
                Box("Crane leg", new(29, 5.8f, 12 + side * 3),
                    new(0.55f, 11.6f, 0.55f), crane, false, "Steel", new(1, 8));
                Box("Crane top beam", new(32, 11.6f, 12 + side * 3),
                    new(13.8f, 0.49f, 0.57f), crane);
                for (int bar = 0; bar < 6; bar++)
                {
                    Transform brace = Box("Crane boom brace", new(26.3f + bar * 2.2f, 12.11f, 12 + side * 3),
                        new(2.65f, 0.12f, 0.11f), steel).transform;
                    brace.localRotation = Quaternion.Euler(0, 0, bar % 2 == 0 ? 24 : -24);
                }
            }
            Box("Crane crossbar", new(29, 11.3f, 12), new(0.59f, 0.59f, 6.4f), crane);
            Box("Crane hoist", new(35, 11.35f, 12), new(1.5f, 0.8f, 6.3f), steel);
            Box("Crane cable", new(35, 8.7f, 12), new(0.04f, 4.85f, 0.04f), steel);
            Box("Crane block", new(35, 6.2f, 12), new(0.26f, 0.42f, 0.26f), crane);

            Sign("CAIS 02", new(23.7f, 1.16f, -7.5f), 0.11f,
                new Color(0.09f, 0.12f, 0.13f), 90);
        }

        internal void Sign(string caption, Vector3 position, float size, Color color, float yaw = 0)
        {
            if (signFont == null)
            {
                signFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                signMaterial = new Material(Resources.Load<Shader>("Harbor/CitySign"));
                signMaterial.mainTexture = signFont.material.mainTexture;
                if (Application.isPlaying)
                    Font.textureRebuilt += RefreshSignAtlas;
            }
            signFont.RequestCharactersInTexture(caption, 72, FontStyle.Bold);
            signMaterial.mainTexture = signFont.material.mainTexture;
            GameObject plaque = new GameObject(caption);
            plaque.transform.SetParent(root, false);
            plaque.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            TextMesh type = plaque.AddComponent<TextMesh>();
            type.font = signFont;
            type.text = caption;
            type.fontSize = 72;
            type.fontStyle = FontStyle.Bold;
            type.alignment = TextAlignment.Center;
            type.anchor = TextAnchor.MiddleCenter;
            type.characterSize = size * 0.23f;
            type.color = color;
            MeshRenderer renderer = plaque.GetComponent<MeshRenderer>();
            if (renderer == null)
                renderer = plaque.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = signMaterial;
        }

        private void RefreshSignAtlas(Font font)
        {
            if (font == signFont && signMaterial != null)
                signMaterial.mainTexture = font.material.mainTexture;
        }

        private void Pad(string name, Vector3 center, Color color)
        {
            Box(name + " pad", center + Vector3.up * 0.023f, new(3.4f, 0.045f, 2.8f),
                new Color(0.18f, 0.24f, 0.27f));
            for (int side = -1; side <= 1; side += 2)
            {
                Box(name + " border", center + new Vector3(side * 1.65f, 0.046f, 0),
                    new(0.1f, 0.04f, 2.8f), color);
                Box(name + " border", center + new Vector3(0, 0.046f, side * 1.35f),
                    new(3.4f, 0.04f, 0.1f), color);
            }
        }

        internal GameObject Box(string name, Vector3 position, Vector3 size, Color color, bool solid = false,
            string surface = null, Vector2 tiling = default, bool bake = true)
        {
            GameObject box = Part(root, PrimitiveType.Cube, name, position, size, color, solid);
            if (surface != null)
                box.GetComponent<Renderer>().sharedMaterial = Surface(surface, color, tiling);
            if (solid && bake)
            {
                walkableGeometry.Add(new NavMeshBuildSource
                {
                    shape = NavMeshBuildSourceShape.Box,
                    transform = Matrix4x4.Translate(box.transform.position),
                    size = size,
                    area = 0
                });
            }
            return box;
        }

        internal void BatchDecoration(string name, Vector3 position, Vector3 size, Color color)
        {
            GameObject part = Box(name, position, size, color);
            var cell = new Vector2Int(Mathf.FloorToInt(position.x / 32f), Mathf.FloorToInt(position.z / 32f));
            var key = (cell, part.GetComponent<MeshRenderer>().sharedMaterial);
            if (!decoration.TryGetValue(key, out List<MeshFilter> pieces))
                decoration.Add(key, pieces = new List<MeshFilter>());
            pieces.Add(part.GetComponent<MeshFilter>());
        }

        internal void FinishDecoration()
        {
            foreach (var entry in decoration)
            {
                List<MeshFilter> pieces = entry.Value;
                if (pieces.Count < 2)
                    continue;

                GameObject batch = new GameObject("Static city detail");
                batch.transform.SetParent(root, false);
                batch.transform.localPosition = new Vector3(entry.Key.Cell.x * 32f, 0, entry.Key.Cell.y * 32f);
                Matrix4x4 inverse = batch.transform.worldToLocalMatrix;
                var sources = new CombineInstance[pieces.Count];
                for (int i = 0; i < pieces.Count; i++)
                {
                    MeshFilter piece = pieces[i];
                    sources[i] = new CombineInstance
                    {
                        mesh = piece.sharedMesh,
                        transform = inverse * piece.transform.localToWorldMatrix
                    };
                }

                var mesh = new Mesh { name = "Static city detail" };
                mesh.CombineMeshes(sources);
                mesh.UploadMeshData(true);
                models.Add(mesh);
                batch.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = batch.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = entry.Key.Material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                foreach (MeshFilter piece in pieces)
                {
                    piece.gameObject.SetActive(false);
                    Object.Destroy(piece.gameObject);
                }
            }
            decoration.Clear();
        }

        private Material Surface(string surface, Color color, Vector2 tiling)
        {
            var key = (surface, color, tiling);
            if (surfaces.TryGetValue(key, out Material material))
                return material;

            var textures = maps[surface];
            material = new Material(surfaceTemplate);
            material.color = color;
            material.SetTexture("_BaseMap", textures.Color);
            material.SetTextureScale("_BaseMap", tiling);
            material.SetFloat("_Smoothness", surface == "Steel" ? 0.39f : 0.16f);
            if (textures.Normal != null)
            {
                material.SetTexture("_BumpMap", textures.Normal);
                material.SetFloat("_BumpScale", 0.65f);
                material.EnableKeyword("_NORMALMAP");
            }
            surfaces.Add(key, material);
            return material;
        }

        internal Transform Model(Transform parent, string name, Color color, params CostaMesh.Section[] section)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            Mesh mesh = CostaMesh.Loft(name, section);
            models.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = SolidMaterial(color);
            return part.transform;
        }

        private Material SolidMaterial(Color color)
        {
            if (!palette.TryGetValue(color, out Material material))
            {
                material = new Material(shader);
                material.color = color;
                material.SetFloat("_Smoothness", 0.22f);
                palette.Add(color, material);
            }
            return material;
        }

        internal GameObject Part(Transform parent, PrimitiveType shape, string name, Vector3 position,
            Vector3 scale, Color color, bool solid = false)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = solid;
                if (!solid && Application.isPlaying)
                    Object.Destroy(collider);
            }
            else if (solid)
            {
                throw new System.InvalidOperationException($"Missing collider for {shape} primitive '{name}'.");
            }

            Renderer renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = SolidMaterial(color);
            if (!solid)
                renderer.shadowCastingMode = parent == root ? ShadowCastingMode.Off : ShadowCastingMode.On;
            return part;
        }
    }
}
