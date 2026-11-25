using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed class CostaJobs
    {
        internal enum Action { Inspect, Checkpoint, Drive, Alert }

        internal readonly struct Step
        {
            internal readonly Vector3 Point;
            internal readonly Action Mode;
            private readonly string english;
            private readonly string portuguese;

            internal string Title => L(english, portuguese);
            internal float Radius => Mode == Action.Drive ? 5.4f : Mode == Action.Checkpoint ? 3.4f : 2.15f;

            internal Step(Vector3 point, Action action, string en, string pt)
            {
                Point = point;
                Mode = action;
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

        internal static readonly Job[] List =
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

        internal static readonly int[] Prices = { 3800, 7200, 12900 };
        private readonly GameObject[] doors;
        private int completed = PlayerPrefs.GetInt("CostaCompleted", 0);
        private int owned = PlayerPrefs.GetInt("CostaOwned", 0);
        private int arsenal = PlayerPrefs.GetInt("CostaArsenal", 0);
        internal static readonly int[] WeaponPrices = { 1200, 2700, 3400, 4900, 6200 };
        private int stage;
        private float deadline;
        private float messageUntil;
        private string noticeEn = "";
        private string noticePt = "";

        internal CostaBag Bag { get; } = CostaBag.Load();
        internal int Credits { get; private set; } = PlayerPrefs.GetInt("CostaCredits", 4500);
        internal int Active { get; private set; } = -1;
        internal bool InProgress => Active >= 0;
        internal Step Current => List[Active].Steps[stage];
        internal int StepNumber => stage + 1;
        internal float Remaining => deadline > 0 ? Mathf.Max(0, deadline - Time.time) : -1;
        internal string Notice => Time.time < messageUntil ? L(noticeEn, noticePt) : "";

        internal CostaJobs(GameObject[] doors)
        {
            this.doors = doors;
            for (int i = 0; i < doors.Length; i++)
                doors[i].SetActive(!Owned(i));
        }

        internal bool Completed(int job) => (completed & (1 << job)) != 0;
        internal bool Owned(int index) => (owned & (1 << index)) != 0;

        internal bool OwnsWeapon(int slot) => slot >= 0 && slot < WeaponPrices.Length && (arsenal & (1 << slot)) != 0;

        internal bool BuyWeapon(int slot)
        {
            if (slot < 0 || slot >= WeaponPrices.Length || OwnsWeapon(slot) || Credits < WeaponPrices[slot])
                return false;

            Credits -= WeaponPrices[slot];
            arsenal |= 1 << slot;
            Save();
            return true;
        }

        internal void PauseDeadline()
        {
            if (deadline > 0)
                deadline += Time.deltaTime;
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
            if (!Near(position, step.Point, step.Radius))
                return false;

            bool reached = step.Mode == Action.Checkpoint || (step.Mode == Action.Drive ? driving : !driving && interact);
            if (!reached)
                return false;

            Job job = List[Active];
            if (stage == 0 && job.TimeLimit > 0)
                deadline = Time.time + job.TimeLimit;

            stage++;
            if (stage == job.Steps.Length)
            {
                if (!Completed(Active))
                {
                    completed |= 1 << Active;
                    Award(job.Reward);
                    Message($"Job complete   + {Money(job.Reward)}", $"Missão concluída   + {Money(job.Reward)}");
                }
                else
                {
                    Message("Job complete", "Missão concluída");
                }
                Select(-1);
            }

            return step.Mode == Action.Alert;
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
            if (Owned(index))
                return false;
            if (Credits < Prices[index])
            {
                Message("Insufficient cash", "Saldo insuficiente");
                return false;
            }

            Credits -= Prices[index];
            owned |= 1 << index;
            doors[index].SetActive(false);
            Save();
            Message($"Vila {index + 1:00} purchased", $"Vila {index + 1:00} comprada");
            return true;
        }

        internal bool AddLoot(int site)
        {
            if (!Bag.AddLoot(site))
                return false;
            Save();
            return true;
        }

        internal void ConfiscateLoot()
        {
            Bag.Clear();
            Save();
        }

        internal void SellLoot(int item)
        {
            int amount = Bag.Sell(item);
            if (amount > 0)
                Award(amount);
        }

        internal void Award(int cash)
        {
            Credits += cash;
            Save();
        }

        private void Message(string en, string pt)
        {
            noticeEn = en;
            noticePt = pt;
            messageUntil = Time.time + 4.8f;
        }

        private void Save()
        {
            PlayerPrefs.SetInt("CostaCredits", Credits);
            PlayerPrefs.SetInt("CostaOwned", owned);
            PlayerPrefs.SetInt("CostaCompleted", completed);
            PlayerPrefs.SetInt("CostaArsenal", arsenal);
            PlayerPrefs.SetString("CostaBag", string.Join(",", Bag.Amounts));
            PlayerPrefs.Save();
        }

        internal static bool Near(Vector3 position, Vector3 target, float radius)
        {
            Vector2 difference = new(position.x - target.x, position.z - target.z);
            return difference.sqrMagnitude <= radius * radius;
        }
    }
}
