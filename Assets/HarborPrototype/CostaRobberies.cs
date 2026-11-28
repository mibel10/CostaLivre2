using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaRobberies
    {
        internal readonly struct Site
        {
            internal readonly Vector3 Point;
            private readonly string english;
            private readonly string portuguese;

            internal string Name => L(english, portuguese);

            internal Site(Vector3 point, string en, string pt)
            {
                Point = point;
                english = en;
                portuguese = pt;
            }
        }

        internal static readonly Site[] Sites =
        {
            new(CostaCity.Hotel + new Vector3(-6, 0, 5), "Grand Hotel Atlântico", "Grand Hotel Atlântico"),
            new(CostaCity.Museum + new Vector3(2.1f, 0, 10), "Santa Luz Museum", "Museu de Santa Luz"),
            new(CostaCity.Jeweler + new Vector3(4, 0, 5), "Áurea Jeweler", "Joalheria Áurea"),
            new(CostaCity.Electronics + new Vector3(2, 0, 7), "Electronics store", "Loja de informática"),
            new(CostaInteriorPlan.Cafe + new Vector3(1.4f, 0, 1.8f), "Praça Café", "Café da Praça"),
            new(new Vector3(-13f, 0, 198f), "North Coast Gas Station", "Posto Costa Norte"),
            new(new Vector3(-69f, 0, 72f), "Central Pharmacy", "Farmácia Central"),
            new(new Vector3(-61f, 0, CostaCity.Districts[0].Center + 12f), "Calado Lottery", "Lotérica Calado"),
            new(new Vector3(-58f, 0, CostaCity.Districts[2].Center + 14f), "Vice Restaurant", "Restaurante Vice")
        };

        private const float Cooldown = 90f;
        internal static Vector3 PickupTarget(int site) => Sites[site].Point + Vector3.up * 0.81f;
        private readonly GameObject[] cases = new GameObject[Sites.Length];
        private readonly float[] available = new float[Sites.Length];
        private float messageUntil;
        private string noticeEn = "";
        private string noticePt = "";

        internal string Notice => Time.time < messageUntil ? L(noticeEn, noticePt) : "";

        internal CostaRobberies(HarborBlockout art)
        {
            for (int i = 0; i < Sites.Length; i++)
            {
                Vector3 point = Sites[i].Point;
                if (i >= 5)
                    BuildStorefront(art, point, i);
                art.Box("Display pedestal", point + Vector3.up * 0.38f, new Vector3(0.78f, 0.76f, 0.69f),
                    new Color(0.22f, 0.25f, 0.24f), true);
                Transform display = art.Box("Loot display tray", point + Vector3.up * 0.78f,
                    new Vector3(0.63f, 0.04f, 0.53f), new Color(0.12f, 0.14f, 0.15f)).transform;
                display.localScale = Vector3.one;
                cases[i] = display.gameObject;
                // Child sizes are in metres. The tray is a separate mesh.
                art.Part(display, PrimitiveType.Cube, "Tray", Vector3.zero, new(0.63f, 0.04f, 0.53f),
                    new Color(0.12f, 0.14f, 0.15f));
                display.GetComponent<Renderer>().enabled = false;
                DressDisplay(art, display, i);
            }
        }

        private static void DressDisplay(HarborBlockout art, Transform display, int site)
        {
            Color gold = HarborBlockout.Amber;
            Color dark = new(0.08f, 0.12f, 0.15f);
            switch (site)
            {
                case 0:
                    art.Part(display, PrimitiveType.Cube, "Gold watch strap", new(-0.14f, 0.05f, 0), new(0.08f, 0.05f, 0.32f), gold);
                    art.Part(display, PrimitiveType.Cylinder, "Watch dial", new(-0.14f, 0.07f, 0.02f), new(0.15f, 0.04f, 0.15f), dark);
                    art.Part(display, PrimitiveType.Capsule, "Silverware", new(0.15f, 0.05f, 0), new(0.07f, 0.05f, 0.27f), Color.gray);
                    break;
                case 1:
                    art.Part(display, PrimitiveType.Cube, "Painting frame", new(-0.14f, 0.19f, 0.08f), new(0.28f, 0.32f, 0.05f), gold);
                    art.Part(display, PrimitiveType.Cube, "Painted canvas", new(-0.14f, 0.19f, 0.048f), new(0.23f, 0.27f, 0.015f), new Color(0.20f, 0.44f, 0.42f));
                    art.Part(display, PrimitiveType.Sphere, "Historic vase", new(0.16f, 0.12f, 0), new(0.15f, 0.21f, 0.15f), new Color(0.65f, 0.36f, 0.18f));
                    break;
                case 2:
                    for (int i = -1; i <= 1; i++)
                    {
                        var gem = art.Part(display, PrimitiveType.Cube, i == 0 ? "Diamond" : "Ruby",
                            new(i * 0.16f, 0.075f, 0), new(0.093f, 0.093f, 0.093f),
                            i == 0 ? new Color(0.76f, 0.95f, 1f) : new Color(0.76f, 0.09f, 0.23f));
                        gem.transform.localRotation = Quaternion.Euler(30, 45, 28);
                    }
                    break;
                case 4:
                    art.Part(display, PrimitiveType.Cube, "Cash register", new(0, 0.08f, 0), new(0.36f, 0.14f, 0.28f), dark);
                    art.Part(display, PrimitiveType.Capsule, "Silver tray", new(0.18f, 0.05f, 0.06f), new(0.08f, 0.04f, 0.22f), Color.gray);
                    break;
                case 5:
                    art.Part(display, PrimitiveType.Cube, "Station safe box", new(0, 0.10f, 0), new(0.38f, 0.18f, 0.32f), new Color(0.28f, 0.32f, 0.36f));
                    art.Part(display, PrimitiveType.Cylinder, "Safe dial", new(0, 0.10f, -0.17f), new(0.10f, 0.02f, 0.10f), gold);
                    break;
                case 6:
                    art.Part(display, PrimitiveType.Cube, "Pharmacy lockbox", new(-0.08f, 0.09f, 0), new(0.32f, 0.14f, 0.24f), new Color(0.92f, 0.94f, 0.96f));
                    art.Part(display, PrimitiveType.Cube, "Red cross", new(-0.08f, 0.17f, 0), new(0.10f, 0.015f, 0.03f), new Color(0.84f, 0.18f, 0.20f));
                    art.Part(display, PrimitiveType.Cube, "Red cross vertical", new(-0.08f, 0.17f, 0), new(0.03f, 0.015f, 0.10f), new Color(0.84f, 0.18f, 0.20f));
                    break;
                case 7:
                    art.Part(display, PrimitiveType.Cube, "Lottery cash case", new(0, 0.08f, 0), new(0.42f, 0.12f, 0.28f), new Color(0.14f, 0.42f, 0.26f));
                    art.Part(display, PrimitiveType.Cube, "Gold band", new(0, 0.145f, 0), new(0.43f, 0.02f, 0.06f), gold);
                    break;
                case 8:
                    art.Part(display, PrimitiveType.Cube, "VIP vault box", new(0, 0.10f, 0), new(0.38f, 0.16f, 0.28f), new Color(0.36f, 0.14f, 0.24f));
                    art.Part(display, PrimitiveType.Cylinder, "Gold watch", new(0.14f, 0.19f, 0), new(0.12f, 0.04f, 0.12f), gold);
                    break;
                default:
                    art.Part(display, PrimitiveType.Cube, "Laptop keyboard", new(0, 0.05f, -0.04f), new(0.39f, 0.04f, 0.26f), dark);
                    art.Part(display, PrimitiveType.Cube, "Laptop screen", new(0, 0.18f, 0.10f), new(0.39f, 0.24f, 0.035f), new Color(0.25f, 0.54f, 0.66f));
                    art.Part(display, PrimitiveType.Cube, "Graphics card", new(0, 0.08f, -0.18f), new(0.21f, 0.035f, 0.085f), Color.gray);
                    break;
            }
        }

        private static void BuildStorefront(HarborBlockout art, Vector3 point, int site)
        {
            Color wall = site switch
            {
                5 => new Color(0.90f, 0.86f, 0.78f),
                6 => new Color(0.88f, 0.94f, 0.92f),
                7 => new Color(0.86f, 0.82f, 0.68f),
                _ => new Color(0.92f, 0.64f, 0.72f)
            };
            Color awning = site switch
            {
                5 => new Color(0.86f, 0.28f, 0.20f),
                6 => new Color(0.14f, 0.62f, 0.44f),
                7 => new Color(0.16f, 0.44f, 0.72f),
                _ => new Color(0.88f, 0.30f, 0.56f)
            };
            string sign = site switch
            {
                5 => "POSTO COSTA NORTE",
                6 => "FARMACIA CENTRAL",
                7 => "LOTERICA CALADO",
                _ => "RESTAURANTE VICE"
            };
            art.MarkFootFloor(art.Box("Robbery shop floor", point + new Vector3(0, 0.03f, 0.4f), new Vector3(7.6f, 0.06f, 6.2f),
                new Color(0.76f, 0.73f, 0.68f), true), CostaAcousticScene.Floor.Stone);
            art.Box("Robbery back wall", point + new Vector3(0, 1.85f, 3.2f), new Vector3(7.6f, 3.7f, 0.28f), wall, true);
            art.Box("Robbery left wall", point + new Vector3(-3.66f, 1.85f, 0.4f), new Vector3(0.28f, 3.7f, 5.8f), wall, true);
            art.Box("Robbery right wall", point + new Vector3(3.66f, 1.85f, 0.4f), new Vector3(0.28f, 3.7f, 5.8f), wall, true);
            art.Box("Robbery roof", point + new Vector3(0, 3.75f, 0.4f), new Vector3(8.0f, 0.26f, 6.6f), new Color(0.26f, 0.28f, 0.30f), true);
            art.Box("Robbery awning", point + new Vector3(0, 3.15f, -2.85f), new Vector3(7.8f, 0.34f, 1.1f), awning, true);
            art.Box("Robbery counter", point + new Vector3(-1.8f, 0.52f, 1.35f), new Vector3(2.8f, 1.04f, 0.72f), new Color(0.42f, 0.29f, 0.20f), true);
            art.Sign(sign, point + new Vector3(0, 3.55f, -2.65f), 0.16f, Color.white);
        }

        internal int Nearby(Vector3 position)
        {
            for (int i = 0; i < Sites.Length; i++)
                if ((position - Sites[i].Point).sqrMagnitude <= 2.05f * 2.05f)
                    return i;
            return -1;
        }

        internal bool Ready(int site, bool[] network = null)
        {
            return network != null && site < network.Length ? network[site] : Time.time >= available[site];
        }

        internal bool Take(int site, CostaJobs account)
        {
            if (!Ready(site))
                return false;
            if (!account.AddLoot(site))
            {
                Message("Not enough space in your backpack.", "Espaço insuficiente na mochila.");
                return false;
            }

            available[site] = Time.time + Cooldown;
            cases[site].SetActive(false);
            Message("Loot in your backpack. Find an exchange.", "Saque na mochila. Procure um posto de venda.");
            return true;
        }

        internal void Refresh(bool[] network = null)
        {
            for (int i = 0; i < cases.Length; i++)
            {
                bool show = Ready(i, network);
                if (cases[i].activeSelf != show)
                    cases[i].SetActive(show);
            }
        }

        private void Message(string en, string pt)
        {
            noticeEn = en;
            noticePt = pt;
            messageUntil = Time.time + 4.8f;
        }
    }
}
