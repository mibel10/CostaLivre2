using System;
using System.Collections.Generic;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    /// <summary>
    /// Universal urban interaction, establishments, collectibles, and per-city reputation/progression.
    /// </summary>
    internal sealed class CostaWorldInteractive
    {
        internal enum NodeKind
        {
            Shop, Food, Gym, Arcade, Bar, Dealer, Bench, Secret, Minigame
        }

        internal sealed class Node
        {
            internal string Id;
            internal NodeKind Kind;
            internal Vector3 Position;
            internal float Radius;
            internal int OpenHour;
            internal int CloseHour;
            internal string TitleEn;
            internal string TitlePt;
            internal string ActionEn;
            internal string ActionPt;
            internal Action OnInteract;
            internal GameObject Visual;
            internal bool Collected;
            internal bool RequiresVehicle;
        }

        private static readonly string[] CityNames = { "Santa Luz", "Calado City", "Antutu City", "Vice City" };
        private readonly List<Node> nodes = new();
        private readonly HarborBlockout art;
        private readonly Transform root;
        private readonly int[] cityRep = new int[4];
        private int collectiblesFound;
        private const int TotalCollectibles = 12;

        internal int CollectiblesFound => collectiblesFound;
        internal int CollectiblesTotal => TotalCollectibles;
        internal IReadOnlyList<Node> Nodes => nodes;

        internal CostaWorldInteractive(HarborBlockout art, Transform parent)
        {
            this.art = art;
            root = new GameObject("Urban interactive establishments").transform;
            root.SetParent(parent, false);
            LoadProgress();
        }

        internal int CityIndex(Vector3 pos)
        {
            if (pos.z < CostaCity.HighwayStart)
                return 0;
            for (int i = 0; i < CostaCity.Districts.Length; i++)
                if (Mathf.Abs(pos.z - CostaCity.Districts[i].Center) < 95f)
                    return i + 1;
            return 0;
        }

        internal int Reputation(int cityIndex) => cityRep[Mathf.Clamp(cityIndex, 0, cityRep.Length - 1)];

        internal string CityTitle(int cityIndex) => CityNames[Mathf.Clamp(cityIndex, 0, CityNames.Length - 1)];

        internal void AddReputation(Vector3 pos, int amount)
        {
            int idx = CityIndex(pos);
            cityRep[idx] = Mathf.Clamp(cityRep[idx] + amount, 0, 1000);
            PlayerPrefs.SetInt($"CostaCityRep_{idx}", cityRep[idx]);
            PlayerPrefs.Save();
        }

        internal float PriceMultiplier(Vector3 pos)
        {
            int rep = Reputation(CityIndex(pos));
            return Mathf.Lerp(1f, 0.75f, Mathf.Clamp01(rep / 500f));
        }

        private void LoadProgress()
        {
            for (int i = 0; i < cityRep.Length; i++)
                cityRep[i] = PlayerPrefs.GetInt($"CostaCityRep_{i}", 0);
            collectiblesFound = 0;
        }

        internal void BuildEstablishments(
            Action<string, int, int, float> onBuyFood,
            Action onStartGym,
            Action onStartArcade,
            Action onStartBasketball,
            Action onStartFishing,
            Action onStartDrift,
            Action onStartStreetRace,
            int savedRelics,
            Func<int, bool> onSecretFound,
            Action<Vector3> onSitBench,
            Action onNightclubDrink,
            Action onDealerUpgrade)
        {
            // 1. Supermercado / Lanchonete "Empório do Cais" (Santa Luz)
            BuildStoreFront(new Vector3(-42f, 0f, 38f), "EMPÓRIO SANTA LUZ",
                new Color(0.82f, 0.78f, 0.68f), new Color(0.22f, 0.48f, 0.36f),
                new Node
                {
                    Id = "food_santaluz", Kind = NodeKind.Food, Position = new Vector3(-42f, 0f, 35.4f),
                    Radius = 2.4f, OpenHour = 6, CloseHour = 23,
                    TitleEn = "Santa Luz Market", TitlePt = "Empório Santa Luz",
                    ActionEn = "Buy snack ($35 · +35 HP)", ActionPt = "Comprar lanche ($35 · +35 Vida)",
                    OnInteract = () => onBuyFood("Lanche & Café", 35, 35, 0.45f)
                });

            // 2. Restaurante "Sabores de Calado" (Calado City)
            BuildStoreFront(new Vector3(-69f, 0f, CostaCity.Districts[0].Center - 15f), "RESTAURANTE CALADO",
                new Color(0.80f, 0.73f, 0.63f), new Color(0.72f, 0.34f, 0.22f),
                new Node
                {
                    Id = "food_calado", Kind = NodeKind.Food, Position = new Vector3(-69f, 0f, CostaCity.Districts[0].Center - 17.6f),
                    Radius = 2.4f, OpenHour = 7, CloseHour = 24,
                    TitleEn = "Calado Diner", TitlePt = "Restaurante Calado",
                    ActionEn = "Order full meal ($65 · +70 HP)", ActionPt = "Pedir prato feito ($65 · +70 Vida)",
                    OnInteract = () => onBuyFood("Prato Costeiro", 65, 70, 0.85f)
                });

            // 3. Academia "Ferro & Mar" (perto do Parque Mirante, Santa Luz)
            BuildGym(new Vector3(31f, 0f, 102f), onStartGym);

            // 4. Fliperama "Arcade Neon" (Vice City)
            BuildArcade(new Vector3(14f, 0f, CostaCity.Districts[2].Center + 12f), onStartArcade);

            // 5. Boate / Bar "Clube Noturno Vice" (Vice City - aberta principalmente à noite)
            BuildNightclub(new Vector3(-72f, 0f, CostaCity.Districts[2].Center - 14f), onNightclubDrink);

            // 6. Concessionária / Oficina Clandestina (Antutu City)
            BuildDealer(new Vector3(18f, 0f, CostaCity.Districts[1].Center + 13f), onDealerUpgrade);

            // 7. Quadra de Basquete Pública (Parque Mirante)
            BuildBasketballCourt(new Vector3(60f, 0f, 108f), onStartBasketball);

            // 8. Píer de Pesca (Cais de Santa Luz)
            BuildFishingSpot(new Vector3(24.2f, 0f, 16f), onStartFishing);

            // 9. Ponto de Drift & Arrancada (Calado City / Rodovia)
            BuildRaceKiosk(new Vector3(-20f, 0f, CostaCity.Districts[0].Center - 6f),
                "DESAFIO DE DRIFT", new Color(0.86f, 0.48f, 0.22f),
                new Node
                {
                    Id = "minigame_drift", Kind = NodeKind.Minigame,
                    Position = new Vector3(-20f, 0f, CostaCity.Districts[0].Center - 8.2f),
                    Radius = 3.2f, OpenHour = 0, CloseHour = 24, RequiresVehicle = true,
                    TitleEn = "Drift Challenge", TitlePt = "Desafio de Drift",
                    ActionEn = "Start Drift Trial (in vehicle)", ActionPt = "Iniciar Drift (no veículo)",
                    OnInteract = onStartDrift
                });

            // 10. Corrida Clandestina de Rua (Antutu City)
            BuildRaceKiosk(new Vector3(-42f, 0f, CostaCity.Districts[1].Center - 6f),
                "CORRIDA NOTURNA", new Color(0.28f, 0.62f, 0.88f),
                new Node
                {
                    Id = "minigame_race", Kind = NodeKind.Minigame,
                    Position = new Vector3(-42f, 0f, CostaCity.Districts[1].Center - 8.2f),
                    Radius = 3.2f, OpenHour = 0, CloseHour = 24, RequiresVehicle = true,
                    TitleEn = "Street Race", TitlePt = "Corrida de Rua",
                    ActionEn = "Start Street Race (in vehicle)", ActionPt = "Iniciar Corrida de Rua (no veículo)",
                    OnInteract = onStartStreetRace
                });

            // 11. Bancos de praça interativos (para sentar e recuperar fôlego/vida devagar)
            Vector3[] benches =
            {
                new(-56f, 0f, 34f), new(-25f, 0f, 52.5f), new(42f, 0f, 96f),
                new(12f, 0f, CostaCity.Districts[1].Center - 15f),
                new(12f, 0f, CostaCity.Districts[2].Center - 15f)
            };
            for (int i = 0; i < benches.Length; i++)
            {
                Vector3 b = benches[i];
                nodes.Add(new Node
                {
                    Id = $"bench_{i}", Kind = NodeKind.Bench, Position = b, Radius = 1.9f,
                    OpenHour = 0, CloseHour = 24,
                    TitleEn = "Park Bench", TitlePt = "Banco da Praça",
                    ActionEn = "Rest (+stamina & calm)", ActionPt = "Descansar (recupera fôlego)",
                    OnInteract = () => onSitBench(b)
                });
            }

            // 12. Colecionáveis secretos espalhados por telhados, becos e terraços
            Vector3[] secrets =
            {
                new(22.8f, 0.15f, 12f), // Parapeito em frente ao guindaste
                new(-130.5f, 0.5f, 65f), // Beco oeste de Santa Luz
                new(-55f, 23.2f, 64f),   // Terraço do Grand Hotel Atlântico
                new(48f, 0.5f, 136f),    // Atrás da roda-gigante
                new(-102f, 0.5f, 150f),  // Fundo da Vila 02
                new(31f, 9.9f, 60f),     // Terraço do Hospital Costeiro
                new(-91f, 0.5f, CostaCity.Districts[0].Center + 48f), // Beco Calado City
                new(28f, 6.6f, CostaCity.Districts[0].Center - 36f),  // Telhado Garagem do Cais
                new(-73f, 16.6f, CostaCity.Districts[1].Center + 16f), // Terraço Centro Tecnológico
                new(28f, 0.5f, CostaCity.Districts[1].Center + 45f),  // Área industrial Antutu
                new(12f, 9.9f, CostaCity.Districts[2].Center - 36f),  // Terraço Apartamentos Mirante
                new(-88f, 0.5f, CostaCity.Districts[2].Center - 48f)  // Beco secreto Vice City
            };

            for (int i = 0; i < secrets.Length; i++)
            {
                string id = $"CostaSecret_{i}";
                bool found = (savedRelics & (1 << i)) != 0 || PlayerPrefs.GetInt(id, 0) != 0;
                if (found)
                    collectiblesFound++;

                GameObject orb = art.Part(root, PrimitiveType.Sphere, $"Relic {i + 1}",
                    secrets[i] + Vector3.up * 0.65f, new Vector3(0.42f, 0.42f, 0.42f),
                    new Color(0.98f, 0.78f, 0.24f), finish: HarborBlockout.Finish.Metal);
                orb.SetActive(!found);

                int idx = i;
                nodes.Add(new Node
                {
                    Id = id, Kind = NodeKind.Secret, Position = secrets[i], Radius = 2.0f,
                    OpenHour = 0, CloseHour = 24,
                    TitleEn = "Hidden Coastal Relic", TitlePt = "Relíquia Costeira Secreta",
                    ActionEn = "Collect relic (+$500)", ActionPt = "Coletar relíquia (+$500)",
                    Visual = orb, Collected = found,
                    OnInteract = () =>
                    {
                        Node n = nodes.Find(x => x.Id == id);
                        if (n == null || n.Collected || !onSecretFound(idx))
                            return;
                        n.Collected = true;
                        if (n.Visual != null)
                            n.Visual.SetActive(false);
                        collectiblesFound++;
                    }
                });
            }
        }

        private void BuildStoreFront(Vector3 pos, string sign, Color wall, Color awning, Node node)
        {
            art.Box(sign + " facade", pos + new Vector3(0f, 1.8f, 0f), new Vector3(5.6f, 3.6f, 4.2f),
                wall, true, "Concrete", new Vector2(2f, 1.5f));
            art.Box(sign + " awning", pos + new Vector3(0f, 2.55f, -2.55f), new Vector3(5.8f, 0.22f, 1.4f), awning);
            art.Box(sign + " counter", pos + new Vector3(0f, 0.48f, -2.35f), new Vector3(2.8f, 0.96f, 0.65f),
                new Color(0.28f, 0.22f, 0.16f), true);
            art.Sign(sign, pos + new Vector3(0f, 3.15f, -2.14f), 0.14f, Color.white);
            nodes.Add(node);
        }

        private void BuildGym(Vector3 pos, Action onStartGym)
        {
            art.Box("Gym floor", pos + new Vector3(0f, 0.04f, 0f), new Vector3(9.5f, 0.08f, 7.5f),
                new Color(0.22f, 0.25f, 0.28f), true);
            art.Box("Gym canopy", pos + new Vector3(0f, 3.1f, 0f), new Vector3(9.6f, 0.22f, 7.6f),
                new Color(0.18f, 0.22f, 0.25f));
            for (int side = -1; side <= 1; side += 2)
                art.Box("Gym post", pos + new Vector3(side * 4.4f, 1.55f, -3.4f), new Vector3(0.3f, 3.1f, 0.3f),
                    HarborBlockout.Amber, true);
            art.Box("Bench press", pos + new Vector3(-1.8f, 0.45f, 0f), new Vector3(0.8f, 0.5f, 1.8f),
                new Color(0.65f, 0.24f, 0.20f), true);
            art.Box("Barbell", pos + new Vector3(-1.8f, 1.05f, -0.5f), new Vector3(2.1f, 0.08f, 0.08f),
                new Color(0.75f, 0.77f, 0.79f), finish: HarborBlockout.Finish.Metal);
            art.Sign("ACADEMIA FERRO & MAR", pos + new Vector3(0f, 3.1f, -3.85f), 0.15f, HarborBlockout.Amber);
            nodes.Add(new Node
            {
                Id = "gym_mirante", Kind = NodeKind.Gym, Position = pos + new Vector3(-1.8f, 0f, -1.6f),
                Radius = 2.6f, OpenHour = 6, CloseHour = 23,
                TitleEn = "Iron & Sea Gym", TitlePt = "Academia Ferro & Mar",
                ActionEn = "Train strength (Minigame)", ActionPt = "Treinar força (Minigame)",
                OnInteract = onStartGym
            });
        }

        private void BuildArcade(Vector3 pos, Action onStartArcade)
        {
            Color neon = new(0.82f, 0.28f, 0.78f);
            art.Box("Arcade kiosk", pos + new Vector3(0f, 1.65f, 0f), new Vector3(5.2f, 3.3f, 3.6f),
                new Color(0.14f, 0.15f, 0.22f), true);
            for (int i = -1; i <= 1; i++)
            {
                art.Box("Arcade cabinet", pos + new Vector3(i * 1.35f, 0.95f, -2.05f), new Vector3(0.75f, 1.9f, 0.72f),
                    new Color(0.18f, 0.20f, 0.30f), true);
                art.Box("Arcade screen", pos + new Vector3(i * 1.35f, 1.35f, -2.42f), new Vector3(0.55f, 0.42f, 0.04f),
                    neon, finish: HarborBlockout.Finish.Glass);
            }
            art.Sign("ARCADE NEON", pos + new Vector3(0f, 2.85f, -1.84f), 0.16f, neon);
            nodes.Add(new Node
            {
                Id = "arcade_vice", Kind = NodeKind.Arcade, Position = pos + new Vector3(0f, 0f, -2.9f),
                Radius = 2.6f, OpenHour = 9, CloseHour = 24,
                TitleEn = "Neon Arcade", TitlePt = "Arcade Neon",
                ActionEn = "Play Retro Shooter ($20)", ActionPt = "Jogar Fliperama ($20)",
                OnInteract = onStartArcade
            });
        }

        private void BuildNightclub(Vector3 pos, Action onNightclubDrink)
        {
            Color club = new(0.68f, 0.22f, 0.56f);
            art.Box("Club stage", pos + new Vector3(0f, 0.12f, 0f), new Vector3(8.2f, 0.24f, 6.2f),
                new Color(0.16f, 0.14f, 0.20f), true);
            art.Box("Club bar", pos + new Vector3(0f, 0.62f, -2.2f), new Vector3(4.2f, 1.0f, 0.7f), club, true);
            art.Sign("BOATE VICE NIGHTS", pos + new Vector3(0f, 2.4f, -3.1f), 0.16f, club);
            nodes.Add(new Node
            {
                Id = "club_vice", Kind = NodeKind.Bar, Position = pos + new Vector3(0f, 0f, -3.2f),
                Radius = 2.7f, OpenHour = 19, CloseHour = 6,
                TitleEn = "Vice Nights Club", TitlePt = "Boate Vice Nights",
                ActionEn = "Order club drink ($45 · +40 HP & Rep)", ActionPt = "Pedir drink ($45 · +40 Vida & Rep)",
                OnInteract = onNightclubDrink
            });
        }

        private void BuildDealer(Vector3 pos, Action onDealerUpgrade)
        {
            Color teal = new(0.22f, 0.68f, 0.62f);
            art.Box("Dealer booth", pos + new Vector3(0f, 1.4f, 0f), new Vector3(4.6f, 2.8f, 3.2f),
                new Color(0.24f, 0.27f, 0.30f), true);
            art.Sign("TUNAGEM ANTUTU", pos + new Vector3(0f, 2.45f, -1.64f), 0.14f, teal);
            nodes.Add(new Node
            {
                Id = "dealer_antutu", Kind = NodeKind.Dealer, Position = pos + new Vector3(0f, 0f, -2.5f),
                Radius = 3.0f, OpenHour = 0, CloseHour = 24,
                TitleEn = "Antutu Tuners", TitlePt = "Tunagem Antutu",
                ActionEn = "Repair and tune vehicle ($120)", ActionPt = "Reparar e ajustar veículo ($120)",
                RequiresVehicle = true,
                OnInteract = onDealerUpgrade
            });
        }

        private void BuildBasketballCourt(Vector3 pos, Action onStartBasketball)
        {
            art.Box("Court surface", pos + new Vector3(0f, 0.035f, 0f), new Vector3(11f, 0.07f, 15f),
                new Color(0.26f, 0.38f, 0.44f), true);
            art.Box("Hoop post", pos + new Vector3(0f, 1.65f, 6.8f), new Vector3(0.22f, 3.3f, 0.22f),
                new Color(0.20f, 0.22f, 0.24f), true);
            art.Box("Backboard", pos + new Vector3(0f, 3.15f, 6.35f), new Vector3(1.65f, 1.05f, 0.08f), Color.white);
            art.Box("Rim", pos + new Vector3(0f, 2.85f, 5.95f), new Vector3(0.55f, 0.06f, 0.55f),
                new Color(0.92f, 0.38f, 0.18f));
            nodes.Add(new Node
            {
                Id = "minigame_basket", Kind = NodeKind.Minigame, Position = pos + new Vector3(0f, 0f, 1.2f),
                Radius = 3.0f, OpenHour = 6, CloseHour = 23,
                TitleEn = "Street Basketball", TitlePt = "Basquete de Rua",
                ActionEn = "Play Free Throw Challenge", ActionPt = "Jogar Desafio de Basquete",
                OnInteract = onStartBasketball
            });
        }

        private void BuildFishingSpot(Vector3 pos, Action onStartFishing)
        {
            art.Box("Fishing crate", pos + new Vector3(-0.9f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.7f),
                new Color(0.45f, 0.32f, 0.20f), true);
            art.Sign("PESCA NO CAIS", pos + new Vector3(-0.9f, 1.3f, 0f), 0.11f, HarborBlockout.Mint, 90f);
            nodes.Add(new Node
            {
                Id = "minigame_fish", Kind = NodeKind.Minigame, Position = pos + new Vector3(-1.3f, 0f, 0f),
                Radius = 2.4f, OpenHour = 0, CloseHour = 24,
                TitleEn = "Quay Fishing", TitlePt = "Pesca no Cais",
                ActionEn = "Cast fishing line", ActionPt = "Pescar no cais",
                OnInteract = onStartFishing
            });
        }

        private void BuildRaceKiosk(Vector3 pos, string title, Color accent, Node node)
        {
            art.Box(title + " arch", pos + new Vector3(0f, 1.8f, 0f), new Vector3(0.4f, 3.6f, 0.4f), accent, true);
            art.Sign(title, pos + new Vector3(0f, 3.1f, -0.35f), 0.14f, accent);
            nodes.Add(node);
        }

        internal static bool IsOpen(Node node, float hour)
        {
            if (node.OpenHour == 0 && node.CloseHour == 24)
                return true;
            if (node.OpenHour < node.CloseHour)
                return hour >= node.OpenHour && hour < node.CloseHour;
            return hour >= node.OpenHour || hour < node.CloseHour;
        }

        internal Node FindNearby(Vector3 playerPos, bool driving)
        {
            Node best = null;
            float bestDist = float.PositiveInfinity;
            for (int i = 0; i < nodes.Count; i++)
            {
                Node n = nodes[i];
                if (n.Collected || n.RequiresVehicle != driving)
                    continue;
                float d = (playerPos - n.Position).sqrMagnitude;
                if (d <= n.Radius * n.Radius && d < bestDist)
                {
                    bestDist = d;
                    best = n;
                }
            }
            return best;
        }

        internal string Prompt(Node node, string useKey, float hour)
        {
            if (node == null)
                return "";
            if (!IsOpen(node, hour))
                return L($"{node.TitleEn} (Opens {node.OpenHour:00}:00)", $"{node.TitlePt} (Abre às {node.OpenHour:00}:00)");
            return $"{useKey} {L(node.ActionEn, node.ActionPt)}";
        }
    }
}
