using UnityEngine;
using Action = CostaLivre.CostaJobs.Action;
using Haul = CostaLivre.CostaJobs.Haul;
using Job = CostaLivre.CostaJobs.Job;
using Step = CostaLivre.CostaJobs.Step;

namespace CostaLivre
{
    internal static class CostaContracts
    {
        private static readonly float Calado = CostaCity.Districts[0].Center;
        private static readonly float Antutu = CostaCity.Districts[1].Center;
        private static readonly float Vice = CostaCity.Districts[2].Center;

        internal static readonly Job[] List =
        {
            new("The Calado Vault", "O Cofre de Calado", 14500, 0,
                At(Calado, -69, 4, Action.Breach, "Override the market vault controls", "Destrave os controles do cofre do mercado"),
                At(Calado, -69, 15, Action.Fight, "Take out the vault detail", "Derrube a equipe do cofre", 3),
                At(Calado, -69, 19, Action.Collect, "Grab the bullion cases", "Pegue as maletas de ouro", 3, loot: Haul.Bullion),
                At(Calado, -49, 10, Action.Fight, "Fight through the reinforcements", "Enfrente os reforços", 4),
                At(Calado, -18, 24, Action.Inspect, "Hand the gold to the buyer", "Entregue o ouro ao comprador")),
            new("Market Hostile Takeover", "Disputa pelo Mercado", 8300, 0,
                At(Calado, 15, -7, Action.Fight, "Clear the rival crew from the stalls", "Elimine a gangue rival das barracas", 4),
                At(Calado, 18, -10, Action.Collect, "Steal the vendor lockboxes", "Roube as caixas dos comerciantes", 2),
                At(Calado, 34, 9, Action.Inspect, "Pass the lockboxes to the broker", "Entregue as caixas ao intermediário")),
            new("Dockside Carjacking", "Tomada de Veículo no Cais", 7200, 0,
                At(Calado, -61, -8, Action.Fight, "Take down the courier car’s escort", "Derrube a escolta do carro de entregas", 2),
                At(Calado, -45, -3, Action.Seize, "Force out the courier car driver", "Renda o motorista do carro de entregas", vehicle: 13),
                At(Calado, 26, 0, Action.DeliverVehicle, "Deliver the stolen courier car", "Entregue o carro de entregas roubado", vehicle: 13)),
            new("The Salt Ledger", "O Livro do Sal", 7900, 0,
                At(Calado, -81, 5, Action.Breach, "Disarm the ledger case alarm", "Desative o alarme da caixa do livro"),
                At(Calado, -75, 17, Action.Collect, "Take the shipping ledger", "Pegue o livro de cargas", loot: Haul.Records),
                At(Calado, -60, 15, Action.Collect, "Steal the second set of records", "Roube o segundo conjunto de registros", loot: Haul.Records),
                At(Calado, -86, -5, Action.Inspect, "Hand the ledgers to the informant", "Entregue os livros ao informante")),
            new("Antutu Blackout", "Apagão em Antutu", 16000, 0,
                At(Antutu, -53, 7, Action.Breach, "Shut down the tech center’s alarm controls", "Desative os controles de alarme do centro tecnológico"),
                At(Antutu, -73, 15, Action.Fight, "Clear the recovery team", "Derrube a equipe de recuperação", 4),
                At(Antutu, -79, 17, Action.Breach, "Release the data vault", "Destrave o cofre de dados"),
                At(Antutu, -72, 20, Action.Collect, "Steal the encrypted cores", "Roube os núcleos criptografados", 3, loot: Haul.Drives),
                At(Antutu, -54, -4, Action.Fight, "Defeat the team covering the exit", "Elimine a equipe que cobre a saída", 3),
                At(Antutu, -16, 17, Action.Inspect, "Get the cores to the extraction team", "Leve os núcleos à equipe de extração")),
            new("Prototype Intercept", "Interceptação do Protótipo", 9600, 0,
                At(Antutu, -54, -6, Action.Collect, "Take the marked keycard", "Pegue o cartão de acesso marcado", loot: Haul.Drives),
                At(Antutu, -45, -3, Action.Seize, "Take over the prototype sedan", "Renda o motorista do sedã protótipo", vehicle: 14),
                At(Antutu, -8, 0, Action.DeliverVehicle, "Bring the prototype to the lab", "Leve o protótipo ao laboratório", vehicle: 14),
                At(Antutu, -8, 7, Action.Fight, "Eliminate the crew tracking the sedan", "Elimine a gangue que rastreou o sedã", 3)),
            new("Server Room Raid", "Invasão aos Servidores", 10800, 0,
                At(Antutu, -73, 11, Action.Breach, "Open the server cases", "Abra as caixas dos servidores"),
                At(Antutu, -73, 19, Action.Collect, "Take the prototype drives", "Roube os discos protótipos", 2, loot: Haul.Drives),
                At(Antutu, -62, 0, Action.Fight, "Break through the response team", "Atravesse a equipe de resposta", 4),
                At(Antutu, -83, -8, Action.Inspect, "Give the drives to the hacker", "Entregue os discos à hacker")),
            new("Antutu Showdown", "Confronto em Antutu", 9900, 0,
                At(Antutu, 9, -5, Action.Fight, "Take down the plaza crew", "Derrube a gangue da praça", 4),
                At(Antutu, 24, 8, Action.Fight, "Take out the crew leader and backup", "Elimine o líder da gangue e seus reforços", 3),
                At(Antutu, 24, 8, Action.Collect, "Recover the bounty token", "Pegue a ficha da recompensa")),
            new("Vice Midnight Vault", "Cofre da Meia-Noite", 18500, 0,
                At(Vice, -72, 4, Action.Breach, "Unlock the Mirante Club vault controls", "Libere os controles do cofre do Clube do Mirante"),
                At(Vice, -72, 15, Action.Fight, "Take out the club’s vault crew", "Elimine a equipe do cofre do clube", 4),
                At(Vice, -77, 18, Action.Collect, "Steal the diamond shipment", "Roube o carregamento de diamantes", 2, loot: Haul.Gems),
                At(Vice, -67, 18, Action.Collect, "Grab the casino reserve cases", "Pegue as maletas da reserva do cassino", 2),
                At(Vice, -52, 8, Action.Fight, "Fight your way past the reinforcements", "Lute para passar pelos reforços", 4),
                At(Vice, -16, 21, Action.Inspect, "Hand over both shipments", "Entregue os dois carregamentos")),
            new("Velvet Room Robbery", "Roubo na Sala de Veludo", 8400, 0,
                At(Vice, -78, 13, Action.Breach, "Release the private display cases", "Destrave as vitrines particulares"),
                At(Vice, -78, 18, Action.Collect, "Steal the ruby trays", "Roube as bandejas de rubis", 2, loot: Haul.Gems),
                At(Vice, -65, 18, Action.Collect, "Take the collector’s necklace", "Pegue o colar do colecionador", loot: Haul.Gems),
                At(Vice, -87, -7, Action.Inspect, "Meet the gem dealer", "Encontre o negociante de joias")),
            new("Mirante Getaway", "Fuga do Mirante", 10500, 0,
                At(Vice, -56, -4, Action.Fight, "Deal with the supercar’s bodyguards", "Elimine os guarda-costas do supercarro", 3),
                At(Vice, -45, -3, Action.Seize, "Carjack the Mirante supercar", "Renda o motorista do supercarro do Mirante", vehicle: 15),
                At(Vice, 34, 0, Action.DeliverVehicle, "Drop off the supercar at the chop shop", "Entregue o supercarro no desmanche", vehicle: 15)),
            new("Last Crew Standing", "A Última Gangue", 9700, 0,
                At(Vice, 25, -7, Action.Fight, "Eliminate the rival crew at the plaza", "Elimine a gangue rival na praça", 4),
                At(Vice, 16, 11, Action.Fight, "Take out the crew’s lieutenant", "Derrube o braço direito da gangue", 3),
                At(Vice, 16, 11, Action.Collect, "Recover the lieutenant’s case", "Pegue a maleta do braço direito")),
            new("The Three-City Score", "O Golpe das Três Cidades", 22600, 0,
                At(Calado, -69, 16, Action.Collect, "Steal the port’s auction case", "Roube a maleta do leilão no cais"),
                At(Antutu, -73, 12, Action.Breach, "Open the prototype display", "Abra a vitrine de protótipos"),
                At(Antutu, -73, 18, Action.Collect, "Take the auction prototype", "Pegue o protótipo do leilão", loot: Haul.Drives),
                At(Vice, -72, 15, Action.Fight, "Take down the auction security team", "Elimine a segurança do leilão", 4),
                At(Vice, -72, 19, Action.Collect, "Steal the final collection", "Roube a coleção final", 2, loot: Haul.Artifact),
                At(Vice, -12, 22, Action.Inspect, "Deliver the auction haul", "Entregue o saque do leilão")),
            new("The Santa Luz Crown Job", "O Golpe da Coroa de Santa Luz", 17100, 0,
                new(CostaCity.Hotel + new Vector3(-5, 0, 4), Action.Collect, "Take the crown’s claim ticket", "Roube o recibo de retirada da coroa", loot: Haul.Records),
                new(CostaCity.Jeweler + new Vector3(3, 0, 3), Action.Breach, "Release the jeweler’s private display", "Destrave a vitrine particular da joalheria"),
                new(CostaCity.Jeweler + new Vector3(3, 0, 6), Action.Collect, "Grab the crown jewels", "Pegue as joias da coroa", 2, loot: Haul.Gems),
                new(CostaCity.Museum + new Vector3(0, 0, 4), Action.Fight, "Eliminate the crew holding the museum", "Elimine a gangue que tomou o museu", 3),
                new(CostaCity.Museum + new Vector3(0, 0, 9), Action.Collect, "Steal the crown from the exhibit", "Roube a coroa da exposição", loot: Haul.Artifact),
                new(new Vector3(-79, 0, 122), Action.Inspect, "Deliver the crown and jewels to the fence", "Entregue a coroa e as joias ao receptador"))
        };

        private static Step At(float city, float x, float z, Action action, string en, string pt, int count = 1,
            int vehicle = -1, Haul loot = Haul.Case)
            => new(new Vector3(x, 0, city + z), action, en, pt, count, vehicle, loot);
    }
}
