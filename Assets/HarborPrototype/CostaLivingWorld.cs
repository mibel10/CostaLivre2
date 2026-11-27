using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    /// <summary>
    /// Proximity-pooled living urban pedestrians and organic dynamic world events.
    /// </summary>
    internal sealed class CostaLivingWorld
    {
        private sealed class Citizen
        {
            internal HarborCharacter Body;
            internal Vector3 Home;
            internal Vector3 Target;
            internal float PauseUntil;
            internal float Speed;
            internal float PanicUntil;
            internal bool Active;
        }

        internal enum EventType { StreetFair, CarMeet, LostPackage, MysteryMerchant, NightParty }

        private readonly Citizen[] citizens = new Citizen[24];
        private readonly Transform root;
        private readonly HarborBlockout art;
        private float nextPoolCheck;
        private float nextEventAt = 18f;
        private float eventEndsAt;
        private GameObject eventProp;

        internal Vector3? ActiveEventPoint { get; private set; }
        internal string ActiveEventTitle { get; private set; } = "";
        internal EventType CurrentEvent { get; private set; }
        internal bool EventClaimed { get; private set; }

        private static readonly Vector3[] Anchors =
        {
            new(-52f, 0f, 44f), new(-34f, 0f, 52f), new(-14f, 0f, 44f), new(8f, 0f, 46f),
            new(40f, 0f, 96f), new(46f, 0f, 114f), new(-94f, 0f, 82f), new(-58f, 0f, 88f),
            new(-66f, 0f, CostaCity.Districts[0].Center - 12f), new(10f, 0f, CostaCity.Districts[0].Center + 12f),
            new(-66f, 0f, CostaCity.Districts[1].Center - 12f), new(12f, 0f, CostaCity.Districts[1].Center + 12f),
            new(-66f, 0f, CostaCity.Districts[2].Center - 12f), new(12f, 0f, CostaCity.Districts[2].Center + 12f),
            new(-24f, 0f, 34f), new(-74f, 0f, 36f), new(-110f, 0f, 64f), new(22f, 0f, 86f),
            new(-42f, 0f, 118f), new(14f, 0f, 134f), new(-84f, 0f, 128f), new(-16f, 0f, 128f),
            new(-34f, 0f, CostaCity.Districts[0].Center + 12f), new(-34f, 0f, CostaCity.Districts[2].Center - 12f)
        };

        internal CostaLivingWorld(HarborBlockout art, Transform parent)
        {
            this.art = art;
            root = new GameObject("Living urban population").transform;
            root.SetParent(parent, false);

            for (int i = 0; i < citizens.Length; i++)
            {
                Vector3 home = Anchors[i % Anchors.Length];
                HarborCharacter body = art.Person($"Citizen {i + 1}", root, home, Color.white);
                body.WearTexture(Resources.Load<Texture2D>($"Harbor/Wardrobe/look-{(i % 20) + 1}"));
                body.Root.gameObject.SetActive(false);
                citizens[i] = new Citizen
                {
                    Body = body,
                    Home = home,
                    Target = home + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)),
                    Speed = Random.Range(1.35f, 1.95f)
                };
            }

            eventProp = art.Box("Dynamic event crate", Vector3.zero, new Vector3(1.1f, 0.7f, 1.1f),
                new Color(0.84f, 0.56f, 0.96f), true);
            eventProp.SetActive(false);
        }

        internal void AlertDanger(Vector3 origin, float radius)
        {
            float rSq = radius * radius;
            for (int i = 0; i < citizens.Length; i++)
            {
                Citizen c = citizens[i];
                if (!c.Active)
                    continue;
                Vector3 delta = c.Body.Root.position - origin;
                if (delta.sqrMagnitude <= rSq)
                {
                    c.PanicUntil = Time.time + 7f;
                    Vector3 away = delta.sqrMagnitude > 0.1f ? delta.normalized : Vector3.forward;
                    c.Target = c.Body.Root.position + away * 22f;
                }
            }
        }

        internal void Update(Vector3 viewer, float hour, bool dangerActive)
        {
            float dt = Time.deltaTime;
            bool checkPool = Time.unscaledTime >= nextPoolCheck;
            if (checkPool)
                nextPoolCheck = Time.unscaledTime + 0.45f;

            bool night = hour >= 20f || hour < 6f;

            for (int i = 0; i < citizens.Length; i++)
            {
                Citizen c = citizens[i];
                if (checkPool)
                {
                    float distSq = (viewer - c.Home).sqrMagnitude;
                    bool shouldShow = distSq < 68f * 68f && (!night || i % 2 == 0);
                    if (c.Active != shouldShow)
                    {
                        c.Active = shouldShow;
                        c.Body.Root.gameObject.SetActive(shouldShow);
                        if (shouldShow)
                            c.Body.Root.position = c.Home;
                    }
                }
                if (!c.Active)
                    continue;

                if (dangerActive && (c.Body.Root.position - viewer).sqrMagnitude < 24f * 24f)
                    c.PanicUntil = Mathf.Max(c.PanicUntil, Time.time + 3.5f);

                bool panicking = Time.time < c.PanicUntil;
                if (!panicking && Time.time < c.PauseUntil)
                {
                    c.Body.Animate(HarborCharacter.Motion.Idle);
                    continue;
                }

                Vector3 pos = c.Body.Root.position;
                Vector3 toTarget = c.Target - pos;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.4f)
                {
                    c.PauseUntil = Time.time + Random.Range(2f, 5.5f);
                    c.Target = c.Home + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                    c.Body.Animate(HarborCharacter.Motion.Idle);
                }
                else
                {
                    float spd = panicking ? 4.8f : c.Speed;
                    Vector3 dir = toTarget.normalized;
                    c.Body.Root.position = pos + dir * (spd * dt);
                    c.Body.Root.rotation = Quaternion.RotateTowards(
                        c.Body.Root.rotation, Quaternion.LookRotation(dir), dt * 320f);
                    c.Body.Animate(panicking ? HarborCharacter.Motion.Run : HarborCharacter.Motion.Walk,
                        panicking ? 1.1f : 0.72f);
                }
            }

            UpdateDynamicEvents(viewer, hour);
        }

        private void UpdateDynamicEvents(Vector3 viewer, float hour)
        {
            if (ActiveEventPoint.HasValue && Time.time >= eventEndsAt)
                ClearEvent();

            if (!ActiveEventPoint.HasValue && Time.time >= nextEventAt)
            {
                SpawnOrganicEvent(viewer, hour);
                nextEventAt = Time.time + 55f;
            }
        }

        private void SpawnOrganicEvent(Vector3 viewer, float hour)
        {
            Vector3[] candidates =
            {
                new(-56f, 0f, 41f), new(42f, 0f, 106f), new(-12f, 0f, 145f),
                new(14f, 0f, CostaCity.Districts[0].Center - 14f),
                new(14f, 0f, CostaCity.Districts[1].Center - 14f),
                new(14f, 0f, CostaCity.Districts[2].Center - 14f)
            };

            Vector3 chosen = candidates[Random.Range(0, candidates.Length)];
            for (int i = 0; i < candidates.Length; i++)
            {
                if ((candidates[i] - viewer).sqrMagnitude > 35f * 35f)
                {
                    chosen = candidates[i];
                    break;
                }
            }

            bool night = hour >= 20f || hour < 5f;
            CurrentEvent = night ? EventType.NightParty : (EventType)Random.Range(0, 4);
            ActiveEventPoint = chosen;
            EventClaimed = false;
            eventEndsAt = Time.time + 75f;
            ActiveEventTitle = CurrentEvent switch
            {
                EventType.StreetFair => L("Street Market Pop-up", "Feira Relâmpago"),
                EventType.CarMeet => L("Underground Car Meet", "Encontro Automotivo"),
                EventType.LostPackage => L("Lost Courier Crate", "Caixa de Carga Perdida"),
                EventType.MysteryMerchant => L("Mystery Merchant", "Comerciante Misterioso"),
                _ => L("Late Night Block Party", "Festa de Rua Noturna")
            };

            eventProp.transform.position = chosen + Vector3.up * 0.35f;
            eventProp.SetActive(true);
        }

        internal bool TryClaimEvent(Vector3 playerPos, out string rewardText, out int cashReward)
        {
            rewardText = "";
            cashReward = 0;
            if (!ActiveEventPoint.HasValue || EventClaimed)
                return false;
            if ((playerPos - ActiveEventPoint.Value).sqrMagnitude > 2.8f * 2.8f)
                return false;

            EventClaimed = true;
            cashReward = CurrentEvent switch
            {
                EventType.LostPackage => 450,
                EventType.MysteryMerchant => 320,
                EventType.CarMeet => 400,
                _ => 280
            };
            rewardText = $"{ActiveEventTitle}: +${cashReward}";
            ClearEvent();
            return true;
        }

        internal string EventPrompt(Vector3 playerPos, string useKey)
        {
            if (!ActiveEventPoint.HasValue || EventClaimed)
                return "";
            if ((playerPos - ActiveEventPoint.Value).sqrMagnitude > 2.8f * 2.8f)
                return "";
            return $"{useKey} {ActiveEventTitle}";
        }

        private void ClearEvent()
        {
            ActiveEventPoint = null;
            ActiveEventTitle = "";
            eventProp.SetActive(false);
        }
    }
}
