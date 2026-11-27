using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaBag
    {
        internal readonly struct Loot
        {
            internal readonly int Price;
            private readonly string english;
            private readonly string portuguese;
            internal string Title => L(english, portuguese);

            internal Loot(string english, string portuguese, int price)
            {
                this.english = english;
                this.portuguese = portuguese;
                Price = price;
            }
        }

        internal const int Capacity = 10;
        internal static readonly Loot[] Catalog =
        {
            new("Gold watch", "Relógio de ouro", 1950), new("Silverware", "Prataria", 625),
            new("Oil painting", "Pintura a óleo", 3150), new("Historic artifact", "Artefato histórico", 2450),
            new("Diamonds", "Diamantes", 3350), new("Rubies", "Rubis", 1350),
            new("Gaming laptop", "Notebook gamer", 1850), new("Graphics card", "Placa de vídeo", 950)
        };
        private static readonly int[][] SiteContents =
        {
            new[] { 0, 1, 1 }, new[] { 2, 3 }, new[] { 4, 5, 5 }, new[] { 6, 7, 7 }
        };

        internal readonly int[] Amounts = new int[Catalog.Length];
        internal int Count { get; private set; }
        internal int Value { get; private set; }

        internal bool CanLoot(int site) => site >= 0 && site < SiteContents.Length && Count + SiteContents[site].Length <= Capacity;

        internal bool AddLoot(int site)
        {
            if (!CanLoot(site))
                return false;

            foreach (int id in SiteContents[site])
            {
                Amounts[id]++;
                Count++;
                Value += Catalog[id].Price;
            }
            return true;
        }

        internal static bool FromSite(int site, int item) => item == -1 || Array.IndexOf(SiteContents[site], item) >= 0;

        internal bool TransferLoot(int site, CostaBag recipient)
        {
            if (!recipient.CanLoot(site))
                return false;
            int first = SiteContents[site][0];
            int last = SiteContents[site][^1];
            int total = SiteContents[site].Length;
            if (Amounts[first] < 1 || Amounts[last] < total - 1)
                return false;
            foreach (int item in SiteContents[site])
                Sell(item);
            return recipient.AddLoot(site);
        }

        internal int Sell(int item)
        {
            if (item == -1)
            {
                int total = Value;
                Clear();
                return total;
            }
            if (item < 0 || item >= Amounts.Length || Amounts[item] == 0)
                return 0;
            Amounts[item]--;
            Count--;
            Value -= Catalog[item].Price;
            return Catalog[item].Price;
        }

        internal void Clear()
        {
            Array.Clear(Amounts, 0, Amounts.Length);
            Count = 0;
            Value = 0;
        }

        internal void Replace(int[] saved)
        {
            if (saved == null)
                return;
            Clear();
            for (int i = 0; i < Mathf.Min(saved.Length, Amounts.Length); i++)
            {
                int amount = Mathf.Clamp(saved[i], 0, Capacity - Count);
                Amounts[i] = amount;
                Count += amount;
                Value += amount * Catalog[i].Price;
            }
        }

        internal static CostaBag Load()
        {
            var bag = new CostaBag();
            string[] parts = PlayerPrefs.GetString("CostaBag", "").Split(',');
            var saved = new int[Catalog.Length];
            for (int i = 0; i < Mathf.Min(parts.Length, saved.Length); i++)
                int.TryParse(parts[i], out saved[i]);
            bag.Replace(saved);
            return bag;
        }
    }
}
