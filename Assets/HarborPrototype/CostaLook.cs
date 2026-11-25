using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaLook
    {
        private readonly HarborCharacter body;
        private readonly GameObject[] police;
        private readonly GameObject[][] options;
        private int current = -1;
        private bool isPolice;

        internal CostaLook(HarborBlockout art, HarborCharacter body)
        {
            this.body = body;
            police = art.PoliceUniform(body);
            options = new[] { BuildHats(art, body), BuildAccessories(art, body) };
            Apply(0, false);
        }

        internal void Apply(int outfit, bool policeRole)
        {
            if (current == outfit && isPolice == policeRole)
                return;
            current = outfit;
            isPolice = policeRole;
            int textureIndex = policeRole ? 4 : outfit % 27;
            body.WearTexture(Resources.Load<Texture2D>($"Harbor/Wardrobe/look-{textureIndex}"));
            foreach (GameObject part in police)
                part.SetActive(policeRole);
            int[] selected = { outfit / 27 % 3, outfit / 81 % 3 };
            for (int category = 0; category < options.Length; category++)
                for (int option = 0; option < options[category].Length; option++)
                    options[category][option].SetActive(!policeRole && selected[category] == option + 1);
        }

        private static GameObject[] BuildHats(HarborBlockout art, HarborCharacter body)
        {
            Transform cap = Attach(body, body.Head, "Cap", new Vector3(0, 1.68f, 0));
            Color charcoal = new(0.125f, 0.18f, 0.17f);
            art.Part(cap, PrimitiveType.Sphere, "Cap crown", new(0, 0.046f, 0.014f), new(0.226f, 0.126f, 0.234f), charcoal);
            art.Part(cap, PrimitiveType.Cube, "Cap visor", new(0, 0.014f, 0.135f), new(0.176f, 0.019f, 0.13f), charcoal);

            Transform hat = Attach(body, body.Head, "Knit hat", new Vector3(0, 1.70f, 0.004f));
            art.Part(hat, PrimitiveType.Sphere, "Knit crown", new(0, 0.046f, 0), new(0.224f, 0.155f, 0.228f),
                new Color(0.35f, 0.17f, 0.14f));
            art.Part(hat, PrimitiveType.Cylinder, "Knit brim", new(0, -0.002f, 0), new(0.228f, 0.024f, 0.23f),
                new Color(0.25f, 0.115f, 0.10f));
            return new[] { cap.gameObject, hat.gameObject };
        }

        private static GameObject[] BuildAccessories(HarborBlockout art, HarborCharacter body)
        {
            Transform visor = Attach(body, body.Head, "Glasses", new Vector3(0, 1.62f, 0.10f));
            Color tint = new(0.10f, 0.12f, 0.13f);
            for (int side = -1; side <= 1; side += 2)
                art.Part(visor, PrimitiveType.Cube, "Lens", new(side * 0.053f, 0, 0),
                    new(0.083f, 0.035f, 0.013f), tint);
            art.Part(visor, PrimitiveType.Cube, "Bridge", Vector3.zero, new(0.044f, 0.009f, 0.011f), tint);

            Transform pouch = Attach(body, body.Chest, "Sling bag", new Vector3(0.17f, 1.20f, 0.12f));
            art.Part(pouch, PrimitiveType.Capsule, "Sling case", new(0, -0.009f, 0.02f),
                new(0.215f, 0.132f, 0.12f), new Color(0.35f, 0.25f, 0.16f));
            Transform strap = art.Part(pouch, PrimitiveType.Cube, "Sling strap", new(-0.142f, 0.12f, 0),
                new(0.043f, 0.35f, 0.022f), tint).transform;
            strap.localRotation = Quaternion.Euler(0, 0, 44);
            return new[] { visor.gameObject, pouch.gameObject };
        }

        private static Transform Attach(HarborCharacter body, Transform bone, string name, Vector3 point)
        {
            Transform anchor = new GameObject(name).transform;
            anchor.SetParent(body.Root, false);
            anchor.localPosition = point;
            anchor.SetParent(bone, true);
            return anchor;
        }
    }
}
