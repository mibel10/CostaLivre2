using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaJobs
    {
        internal enum Action { Inspect, Checkpoint, Drive, Alert, Breach, Collect, Fight, Seize, DeliverVehicle }
        internal enum Haul { Case, Bullion, Gems, Drives, Records, Artifact }
        internal const int OriginalCount = 6;

        internal readonly struct Step
        {
            internal readonly Vector3 Point;
            internal readonly Action Mode;
            internal readonly int Count;
            internal readonly int Vehicle;
            internal readonly Haul Loot;
            private readonly string english;
            private readonly string portuguese;

            internal string Title => L(english, portuguese);
            internal float Radius => Mode switch
            {
                Action.Drive or Action.DeliverVehicle => 5.4f,
                Action.Checkpoint => 3.4f,
                Action.Seize => 3.25f,
                _ => 2.15f
            };

            internal Step(Vector3 point, Action action, string en, string pt, int count = 1, int vehicle = -1,
                Haul loot = Haul.Case)
            {
                Point = point;
                Mode = action;
                Count = count;
                Vehicle = vehicle;
                Loot = loot;
                english = en;
                portuguese = pt;
            }
        }

        internal sealed class Job
        {
            internal readonly int Reward;
            internal readonly float TimeLimit;
            internal readonly Step[] Steps;
            private readonly string english;
            private readonly string portuguese;

            internal string Title => L(english, portuguese);

            internal Job(string en, string pt, int reward, float limit, params Step[] steps)
            {
                english = en;
                portuguese = pt;
                Reward = reward;
                TimeLimit = limit;
                Steps = steps;
            }
        }

        private static readonly Job[] Original =
        {
            new("Hotel courier", "Entrega no hotel", 3500, 0,
                new(CostaCity.CitySpawn + Vector3.left * 4, Action.Inspect, "Pick up the guest package", "Recolha o pacote do hóspede"),
                new(CostaCity.HotelDrop, Action.Inspect, "Hand it in at hotel reception", "Entregue na recepção do hotel")),
            new("Museum inventory", "Inventário do museu", 3900, 0,
                new(CostaCity.Museum + new Vector3(-7, 0, 4), Action.Inspect, "Scan the bronze sculpture", "Catalogue a escultura de bronze"),
                new(CostaCity.Museum + new Vector3(0, 0, 4), Action.Inspect, "Scan the center exhibit", "Catalogue a peça central"),
                new(CostaCity.Museum + new Vector3(7, 0, 4), Action.Inspect, "File the final inventory entry", "Registre a última peça")),
            new("City sprint", "Circuito de Santa Luz", 5200, 80,
                new(new Vector3(-29, 0, 31), Action.Drive, "Drive through the start", "Passe de carro pela largada"),
                new(new Vector3(-30, 0, 76), Action.Drive, "Reach the boulevard checkpoint", "Passe pelo ponto na avenida"),
                new(new Vector3(18, 0, 76), Action.Drive, "Cross the eastern intersection", "Cruze o entroncamento leste"),
                new(new Vector3(18, 0, 123), Action.Drive, "Follow the road to Mirante", "Siga pela avenida do Mirante"),
                new(new Vector3(65, 0, 123), Action.Drive, "Finish by the amusement park", "Termine ao lado do parque")),
            new("Aurea display", "Vitrine Áurea", 4500, 65,
                new(CostaCity.Jeweler, Action.Inspect, "Collect the display sample", "Retire a amostra da vitrine"),
                new(CostaCity.Gallery, Action.Inspect, "Deliver the sample to the museum", "Leve a amostra ao museu")),
            new("Station recovery", "Recuperação na delegacia", 6100, 0,
                new(CostaCity.Station + new Vector3(0, 0, 4), Action.Alert, "Collect the marked evidence case", "Recolha a maleta de provas marcada"),
                new(HarborBlockout.Safehouse, Action.Checkpoint, "Evade the responding officers", "Despiste os policiais até o esconderijo")),
            new("Property survey", "Vistoria de imóveis", 4300, 0,
                new(CostaCity.Estate[0] + Vector3.back * 4, Action.Inspect, "Inspect Vila 01", "Vistorie a Vila 01"),
                new(CostaCity.Estate[1] + Vector3.back * 4, Action.Inspect, "Inspect Vila 02", "Vistorie a Vila 02"),
                new(CostaCity.Estate[2] + Vector3.back * 4, Action.Inspect, "Submit the final property report", "Entregue o laudo da última vila"))
        };

        internal static readonly Job[] List = BuildList();

        private static Job[] BuildList()
        {
            Job[] list = new Job[Original.Length + CostaContracts.List.Length];
            Original.CopyTo(list, 0);
            CostaContracts.List.CopyTo(list, Original.Length);
            return list;
        }

        internal static readonly int[] Prices = { 3800, 7200, 12900 };
        private GameObject[] doors = System.Array.Empty<GameObject>();
        internal static readonly int[] WeaponPrices = { 1200, 2700, 3400, 4900, 6200 };
        private int stage;
        private float deadline;
        private float messageUntil;
        private string noticeEn = "";
        private string noticePt = "";

        internal CostaBag Bag { get; }
        internal readonly CostaLedger Ledger = new(System.IO.Path.Combine(Application.persistentDataPath,"CostaBank-v1.json"));
        internal CostaLedger.Account Account { get; }

        internal CostaJobs()
        {
            Bag = CostaBag.Load();
            Account = Ledger.Get("solo", PlayerPrefs.GetInt("CostaCredits",4500));
            Ledger.MigrateSolo(Account, PlayerPrefs.GetInt("CostaArsenal",0),
                PlayerPrefs.GetInt("CostaOwned",0), PlayerPrefs.GetInt("CostaCompleted",0)
                    | (PlayerPrefs.GetInt("CostaPortPaid",0) != 0 ? PortCompletion : 0), Bag.Amounts);
            Bag.Replace(Account.soloBag);
        }
        internal int DailyPropertyCost => ((Owned(0)?1:0)+(Owned(1)?1:0)+(Owned(2)?1:0))*100;
        internal static long BankDay => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()/86400;
        internal int Credits => Account.cash;
        internal int Active { get; private set; } = -1;
        internal bool InProgress => Active >= 0;
        internal Step Current => List[Active].Steps[stage];
        internal int StepNumber => stage + 1;
        internal float Remaining => deadline > 0 ? Mathf.Max(0, deadline - Time.time) : -1;
        internal string Notice => Time.time < messageUntil ? L(noticeEn, noticePt) : "";

        internal void AttachDoors(GameObject[] entrances)
        {
            doors = entrances;
            SetOnlineDoors(false);
        }

        internal void SetOnlineDoors(bool online)
        {
            for (int i = 0; i < doors.Length; i++)
                SetDoorObstacle(doors[i], online || !Owned(i));
        }

        private static void SetDoorObstacle(GameObject door, bool solid)
        {
            door.GetComponent<Collider>().enabled = solid;
            door.GetComponent<UnityEngine.AI.NavMeshObstacle>().enabled = solid;
        }

        internal bool Completed(int job) => (Account.completedJobs & (1 << job)) != 0;
        internal bool Owned(int index) => (Account.properties & (1 << index)) != 0;

        internal bool OwnsWeapon(int slot) => slot >= 0 && slot < WeaponPrices.Length && (Account.weapons & (1 << slot)) != 0;

        internal bool BuyWeapon(int slot)
        {
            if (slot < 0 || slot >= WeaponPrices.Length || OwnsWeapon(slot) || Credits < WeaponPrices[slot])
                return false;

            return Ledger.Apply(Account,CostaLedger.Operation.Purchase,WeaponPrices[slot],weapon:slot);
        }

        private float pausedAt;

        internal void PauseForSession() => pausedAt = Time.time;

        internal void ResumeAfterSession()
        {
            if (deadline > 0)
                deadline += Time.time - pausedAt;
        }

        internal void Select(int job)
        {
            Active = job;
            stage = 0;
            deadline = 0;
        }

        internal void CheckClock()
        {
            if (InProgress && deadline > 0 && Time.time >= deadline)
            {
                Select(-1);
                Message("Time expired. Restart the job from the board.", "Tempo esgotado. Reinicie a missão no quadro.");
            }
        }

        internal bool Advance(Vector3 position, bool driving, bool interact)
        {
            if (!InProgress)
                return false;

            Step step = Current;
            if (step.Mode > Action.Alert || !Near(position, step.Point, step.Radius))
                return false;

            bool reached = step.Mode == Action.Checkpoint || (step.Mode == Action.Drive ? driving : !driving && interact);
            if (!reached)
                return false;

            CompleteStep();
            return step.Mode == Action.Alert;
        }

        internal int CompletionSerial { get; private set; }

        internal void CompleteStep()
        {
            Job job = List[Active];
            if (stage == 0 && job.TimeLimit > 0)
                deadline = Time.time + job.TimeLimit;

            stage++;
            if (stage == job.Steps.Length)
            {
                if (!Completed(Active))
                {
                    if (!Ledger.SaveSolo(Account, job.Reward, completed: 1 << Active))
                    {
                        Message("Reward could not be saved. Retry the contract.", "A recompensa não foi salva. Repita o contrato.");
                        Select(-1);
                        return;
                    }
                    Message($"Job complete   + {Money(job.Reward)}", $"Missão concluída   + {Money(job.Reward)}");
                }
                else
                {
                    Message("Job complete", "Missão concluída");
                }
                CompletionSerial++;
                Select(-1);
            }
        }

        internal int NearbyProperty(Vector3 position)
        {
            for (int i = 0; i < CostaCity.Estate.Length; i++)
            {
                if (Near(position, CostaCity.Estate[i] + Vector3.back * 3.1f, 2.45f))
                    return i;
            }
            return -1;
        }

        internal bool Buy(int index)
        {
            if (index < 0 || index >= Prices.Length || index >= doors.Length || Owned(index))
                return false;
            if (Credits < Prices[index])
            {
                Message("Insufficient cash", "Saldo insuficiente");
                return false;
            }

            if (!Ledger.SaveSolo(Account, -Prices[index], property: 1 << index)) return false;
            SetDoorObstacle(doors[index], false);
            Message($"Vila {index + 1:00} purchased", $"Vila {index + 1:00} comprada");
            return true;
        }

        internal bool AddLoot(int site)
        {
            if (!Bag.AddLoot(site))
                return false;
            if (Ledger.SaveSolo(Account, bag: Bag.Amounts))
                return true;
            Bag.Replace(Account.soloBag);
            Message("Backpack could not be saved.", "Não foi possível salvar a mochila.");
            return false;
        }

        internal void ConfiscateLoot()
        {
            Bag.Clear();
            if (!Ledger.SaveSolo(Account, bag: Bag.Amounts))
                Bag.Replace(Account.soloBag);
        }

        internal void SellLoot(int item)
        {
            if (Bag.PriceOfSale(item) <= 0)
                return;
            int amount = Bag.Sell(item);
            if (Ledger.SaveSolo(Account, amount, bag: Bag.Amounts))
                return;
            Bag.Replace(Account.soloBag);
            Message("Sale failed. Your loot is still in the backpack.", "A venda falhou. O saque continua na mochila.");
        }

        // The contract board has fewer than 31 entries. Keep the legacy port tutorial in its own bit.
        private const int PortCompletion = int.MinValue;
        internal bool FinishPort()
        {
            if ((Account.completedJobs & PortCompletion) != 0
                || Ledger.SaveSolo(Account, 3200, completed: PortCompletion))
                return true;
            Message("Save failed. Retrying…", "Falha ao salvar. Tentando novamente…");
            return false;
        }

        private void Message(string en, string pt)
        {
            noticeEn = en;
            noticePt = pt;
            messageUntil = Time.time + 4.8f;
        }

        internal static bool Near(Vector3 position, Vector3 target, float radius)
        {
            Vector2 difference = new(position.x - target.x, position.z - target.z);
            return difference.sqrMagnitude <= radius * radius;
        }
    }
}
