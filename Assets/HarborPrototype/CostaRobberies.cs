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
            new(CostaCity.Electronics + new Vector3(2, 0, 7), "Electronics store", "Loja de informática")
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
                default:
                    art.Part(display, PrimitiveType.Cube, "Laptop keyboard", new(0, 0.05f, -0.04f), new(0.39f, 0.04f, 0.26f), dark);
                    art.Part(display, PrimitiveType.Cube, "Laptop screen", new(0, 0.18f, 0.10f), new(0.39f, 0.24f, 0.035f), new Color(0.25f, 0.54f, 0.66f));
                    art.Part(display, PrimitiveType.Cube, "Graphics card", new(0, 0.08f, -0.18f), new(0.21f, 0.035f, 0.085f), Color.gray);
                    break;
            }
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
