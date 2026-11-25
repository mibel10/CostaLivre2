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
                cases[i] = art.Box("Robbery loot", point + Vector3.up * 0.90f, new Vector3(0.53f, 0.28f, 0.42f),
                    HarborBlockout.Amber);
            }
        }

        internal int Nearby(Vector3 position)
        {
            for (int i = 0; i < Sites.Length; i++)
                if (CostaJobs.Near(position, Sites[i].Point, 2.05f))
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
