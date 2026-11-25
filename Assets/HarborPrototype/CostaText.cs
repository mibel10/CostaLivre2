using System.Globalization;
using UnityEngine;

namespace CostaLivre
{
    internal static class CostaText
    {
        internal static bool Portuguese { get; private set; } = PlayerPrefs.GetInt("CostaPortuguese", 1) != 0;

        private static readonly CultureInfo BrazilianCurrency = CultureInfo.GetCultureInfo("pt-BR");
        private static readonly CultureInfo EnglishNumbers = CultureInfo.GetCultureInfo("en-US");

        internal static string L(string en, string pt) => Portuguese ? pt : en;
        internal static string Money(int amount) => "R$ " + amount.ToString("N0", Portuguese ? BrazilianCurrency : EnglishNumbers);

        internal static void Language(bool portuguese)
        {
            Portuguese = portuguese;
            PlayerPrefs.SetInt("CostaPortuguese", portuguese ? 1 : 0);
            PlayerPrefs.Save();
        }

        internal static string Weapon(int slot)
        {
            switch (slot)
            {
                case 0: return L("Pistol", "Pistola");
                case 1: return L("Shotgun", "Escopeta");
                case 2: return "SMG";
                case 3: return "Rifle";
                default: return L("Sniper", "Precisão");
            }
        }
    }
}
