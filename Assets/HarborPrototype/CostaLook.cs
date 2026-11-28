using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaLook
    {
        private readonly HarborCharacter body;
        private readonly GameObject[] police;
        private readonly GameObject[][] options;
        private readonly GameObject[] civilianDetails;
        private readonly GameObject hair;
        private int current = -1;
        private bool isPolice;
        private int lastTexture = -1;

        internal CostaLook(HarborBlockout art, HarborCharacter body)
        {
            this.body = body;
            police = art.PoliceUniform(body);
            civilianDetails = BuildCivilianDetails(art, body);
            hair = BuildHair(art, body);
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
            if (lastTexture != textureIndex)
            {
                lastTexture = textureIndex;
                body.WearTexture(Resources.Load<Texture2D>($"Harbor/Wardrobe/look-{textureIndex}"));
            }
            foreach (GameObject part in civilianDetails)
                if (part != null)
                    part.SetActive(!policeRole);
            foreach (GameObject part in police)
                part.SetActive(policeRole);
            bool wearingHat = false;
            for (int category = 0; category < options.Length; category++)
            {
                int selected = outfit / (category == 0 ? 27 : 81) % 3;
                if (category == 0 && !policeRole && selected > 0)
                    wearingHat = true;
                for (int option = 0; option < options[category].Length; option++)
                {
                    GameObject detail = options[category][option];
                    bool visible = !policeRole && selected == option + 1;
                    if (detail.activeSelf != visible)
                        detail.SetActive(visible);
                }
            }
            if (hair != null)
                hair.SetActive(!policeRole && !wearingHat);
        }

        private static GameObject BuildHair(HarborBlockout art, HarborCharacter body)
        {
            Transform scalp = Attach(body, body.Head, "Sculpted hair", new Vector3(0, 1.675f, -0.008f));
            Color darkHair = new(0.11f, 0.085f, 0.072f);
            Color hairHighlight = new(0.16f, 0.125f, 0.105f);
            // Layered modern tropical fade + textured top volume
            art.Part(scalp, PrimitiveType.Sphere, "Hair base", new(0, 0.025f, -0.005f),
                new(0.214f, 0.145f, 0.224f), darkHair);
            var quiff = art.Part(scalp, PrimitiveType.Sphere, "Hair top volume", new(0, 0.068f, 0.026f),
                new(0.175f, 0.095f, 0.195f), hairHighlight).transform;
            quiff.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            art.Part(scalp, PrimitiveType.Cube, "Hair nape", new(0, -0.028f, -0.092f),
                new(0.142f, 0.082f, 0.045f), darkHair);
            return scalp.gameObject;
        }

        private static GameObject[] BuildCivilianDetails(HarborBlockout art, HarborCharacter body)
        {
            var parts = new System.Collections.Generic.List<GameObject>(4);

            // Facial eyebrows for sharper expression (stay visible in all roles)
            Transform brows = Attach(body, body.Head, "Eyebrows", new Vector3(0, 1.635f, 0.096f));
            Color browColor = new(0.12f, 0.09f, 0.075f);
            for (int side = -1; side <= 1; side += 2)
            {
                var brow = art.Part(brows, PrimitiveType.Cube, "Eyebrow", new(side * 0.036f, 0, 0),
                    new(0.036f, 0.009f, 0.012f), browColor).transform;
                brow.localRotation = Quaternion.Euler(0, side * 14f, side * -5f);
            }

            // Layered open tropical overshirt / jacket + gold chain + belt
            Transform vest = Attach(body, body.Chest, "Tailored jacket details", new Vector3(0, 1.26f, 0.129f));
            Color fabric = new(0.15f, 0.19f, 0.22f);
            Color seam = new(0.32f, 0.36f, 0.38f);
            Color gold = new(0.88f, 0.70f, 0.32f);
            Color leather = new(0.22f, 0.15f, 0.11f);
            art.Part(vest, PrimitiveType.Cube, "Recessed front seam", new(0, -0.055f, 0.016f),
                new(0.017f, 0.28f, 0.012f), fabric);
            art.Part(vest, PrimitiveType.Cube, "Zipper pull", new(0, 0.073f, 0.027f),
                new(0.012f, 0.026f, 0.012f), gold, finish: HarborBlockout.Finish.Metal);

            // Gold chain necklace
            var chain = art.Part(vest, PrimitiveType.Cylinder, "Gold chain", new(0, 0.176f, -0.022f),
                new(0.148f, 0.009f, 0.148f), gold, finish: HarborBlockout.Finish.Metal).transform;
            chain.localRotation = Quaternion.Euler(14f, 0f, 0f);
            var pendant = art.Part(vest, PrimitiveType.Cube, "Chain pendant", new(0, 0.132f, 0.038f),
                new(0.018f, 0.024f, 0.009f), gold, finish: HarborBlockout.Finish.Metal).transform;
            body.RegisterAttachment(pendant, 0.45f);

            // Belt & metallic buckle at lower torso
            art.Part(vest, PrimitiveType.Cube, "Belt strap", new(0, -0.255f, -0.122f),
                new(0.335f, 0.044f, 0.235f), leather);
            art.Part(vest, PrimitiveType.Cube, "Belt buckle", new(0, -0.255f, 0.002f),
                new(0.054f, 0.046f, 0.020f), gold, finish: HarborBlockout.Finish.Metal);

            for (int side = -1; side <= 1; side += 2)
            {
                var collar = art.Part(vest, PrimitiveType.Cube, "Collar facing",
                    new(side * 0.062f, 0.168f, 0.008f), new(0.052f, 0.105f, 0.028f), fabric).transform;
                collar.localRotation = Quaternion.Euler(0, side * 11, side * -26);
                body.RegisterAttachment(collar, 0.22f);
                var lapel = art.Part(vest, PrimitiveType.Cube, "Jacket lapel",
                    new(side * 0.072f, 0.055f, 0.014f), new(0.044f, 0.13f, 0.018f), seam).transform;
                lapel.localRotation = Quaternion.Euler(0, side * 8, side * 12);
                var pocket = art.Part(vest, PrimitiveType.Cube, "Pocket welt",
                    new(side * 0.105f, -0.098f, 0.008f), new(0.082f, 0.020f, 0.020f), seam).transform;
                pocket.localRotation = Quaternion.Euler(0, side * 11, side * 15);
                art.Part(vest, PrimitiveType.Cube, "Pocket button",
                    new(side * 0.105f, -0.086f, 0.020f), new(0.012f, 0.012f, 0.008f), gold, finish: HarborBlockout.Finish.Metal);
            }

            parts.Add(vest.gameObject);

            // Wristwatch on left forearm/wrist
            if (body.LeftForearm != null && body.LeftHand != null)
            {
                Vector3 wristPos = Vector3.Lerp(body.LeftForearm.position, body.LeftHand.position, 0.84f);
                Transform watch = new GameObject("Wristwatch").transform;
                watch.SetParent(body.LeftForearm, false);
                watch.position = wristPos;
                watch.rotation = body.LeftForearm.rotation;
                art.Part(watch, PrimitiveType.Cylinder, "Watch band", Vector3.zero,
                    new(0.076f, 0.016f, 0.076f), gold, finish: HarborBlockout.Finish.Metal);
                art.Part(watch, PrimitiveType.Cylinder, "Watch dial", new(0, 0, 0.028f),
                    new(0.042f, 0.012f, 0.042f), new Color(0.07f, 0.14f, 0.18f), finish: HarborBlockout.Finish.Glass);
                parts.Add(watch.gameObject);
            }

            // Clean coastal white sneaker soles on both feet
            Color sneakerSole = new(0.94f, 0.92f, 0.88f);
            if (body.LeftFoot != null)
                parts.Add(art.Part(body.LeftFoot, PrimitiveType.Cube, "Left sneaker sole", new(0, -0.032f, 0.035f),
                    new(0.105f, 0.030f, 0.245f), sneakerSole));
            if (body.RightFoot != null)
                parts.Add(art.Part(body.RightFoot, PrimitiveType.Cube, "Right sneaker sole", new(0, -0.032f, 0.035f),
                    new(0.105f, 0.030f, 0.245f), sneakerSole));

            return parts.ToArray();
        }

        private static GameObject[] BuildHats(HarborBlockout art, HarborCharacter body)
        {
            Transform cap = Attach(body, body.Head, "Tropical cap", new Vector3(0, 1.68f, 0));
            Color teal = new(0.12f, 0.44f, 0.48f);
            Color cream = new(0.94f, 0.90f, 0.82f);
            Color coral = new(0.90f, 0.42f, 0.36f);
            art.Part(cap, PrimitiveType.Sphere, "Cap crown", new(0, 0.046f, 0.012f), new(0.228f, 0.130f, 0.236f), teal);
            art.Part(cap, PrimitiveType.Cube, "Cap visor", new(0, 0.014f, 0.138f), new(0.180f, 0.018f, 0.138f), coral);
            art.Part(cap, PrimitiveType.Cube, "Cap emblem", new(0, 0.056f, 0.118f), new(0.052f, 0.036f, 0.014f), cream);

            Transform hat = Attach(body, body.Head, "Coastal bucket hat", new Vector3(0, 1.70f, 0.004f));
            art.Part(hat, PrimitiveType.Sphere, "Bucket crown", new(0, 0.044f, 0), new(0.226f, 0.142f, 0.230f), cream);
            art.Part(hat, PrimitiveType.Cylinder, "Bucket band", new(0, 0.014f, 0), new(0.224f, 0.018f, 0.226f), teal);
            art.Part(hat, PrimitiveType.Cylinder, "Bucket brim", new(0, -0.006f, 0), new(0.315f, 0.014f, 0.315f), cream);
            return new[] { cap.gameObject, hat.gameObject };
        }

        private static GameObject[] BuildAccessories(HarborBlockout art, HarborCharacter body)
        {
            Transform visor = Attach(body, body.Head, "Aviator glasses", new Vector3(0, 1.615f, 0.102f));
            Color lensTint = new(0.08f, 0.14f, 0.18f);
            Color gold = new(0.88f, 0.70f, 0.32f);
            for (int side = -1; side <= 1; side += 2)
            {
                art.Part(visor, PrimitiveType.Cube, "Lens", new(side * 0.052f, -0.003f, 0),
                    new(0.078f, 0.040f, 0.010f), lensTint, finish: HarborBlockout.Finish.Glass);
                art.Part(visor, PrimitiveType.Cube, "Rim", new(side * 0.052f, 0.019f, 0),
                    new(0.082f, 0.007f, 0.012f), gold, finish: HarborBlockout.Finish.Metal);
                art.Part(visor, PrimitiveType.Cube, "Temple arm", new(side * 0.095f, 0.006f, -0.065f),
                    new(0.008f, 0.009f, 0.135f), gold, finish: HarborBlockout.Finish.Metal);
            }
            art.Part(visor, PrimitiveType.Cube, "Bridge", new(0, 0.012f, 0), new(0.044f, 0.008f, 0.010f), gold, finish: HarborBlockout.Finish.Metal);

            Transform pouch = Attach(body, body.Chest, "Sling bag", new Vector3(0.17f, 1.20f, 0.12f));
            art.Part(pouch, PrimitiveType.Capsule, "Sling case", new(0, -0.009f, 0.02f),
                new(0.215f, 0.135f, 0.12f), new Color(0.20f, 0.24f, 0.26f));
            art.Part(pouch, PrimitiveType.Cube, "Sling zip", new(0, 0.025f, 0.082f),
                new(0.145f, 0.014f, 0.012f), gold, finish: HarborBlockout.Finish.Metal);
            body.RegisterAttachment(pouch, 0.8f);
            Transform strap = art.Part(pouch, PrimitiveType.Cube, "Sling strap", new(-0.142f, 0.12f, 0),
                new(0.044f, 0.36f, 0.022f), new Color(0.12f, 0.14f, 0.16f)).transform;
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
