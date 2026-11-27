using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    internal sealed partial class CostaSession
    {
        [Serializable]
        internal sealed class Dispatch
        {
            public bool active;
            public int site;
            public bool guard;
        }

        [Serializable]
        internal sealed class CityOpportunity
        {
            public bool active;
            public float remaining;
            public int site;
            public bool falseAlarm;
        }

        private CityOpportunity opportunity;
        private float opportunityEnds;
        private float nextOpportunity;
        private bool nextFalseAlarm = true;
        internal CityOpportunity Opportunity { get; private set; }

        internal bool TryMapOpportunity(out Vector3 point, out string title)
        {
            point = default;
            title = "";
            if (!Online || Opportunity == null || Local.police != Opportunity.falseAlarm)
                return false;
            point = Entrance(Opportunity.site);
            title = Opportunity.falseAlarm ? L("Alarm report", "Alarme reportado")
                : L("Vulnerable store", "Loja vulnerável");
            return true;
        }

        private void TickOpportunity(float now)
        {
            if (opportunity != null && (now >= opportunityEnds || opportunity.falseAlarm && RobberyAlarm(opportunity.site, now)))
                EndOpportunity(now);
            if (opportunity == null && CountPlayers() > 0)
            {
                if (nextOpportunity == 0)
                    nextOpportunity = now + 45;
                else if (now >= nextOpportunity)
                {
                    int first = UnityEngine.Random.Range(0, robbers.Length);
                    for (int offset = 0; offset < robbers.Length; offset++)
                    {
                        int site = (first + offset) % robbers.Length;
                        if (robbers[site] != null || !nextFalseAlarm && now < restock[site])
                            continue;
                        opportunity = new CityOpportunity { active = true, site = site, falseAlarm = nextFalseAlarm };
                        opportunityEnds = now + 75;
                        nextFalseAlarm = !nextFalseAlarm;
                        break;
                    }
                    nextOpportunity = now + 8;
                }
            }
            if (opportunity != null)
                opportunity.remaining = Mathf.Max(0, opportunityEnds - now);
            if (opportunity?.falseAlarm != true)
                return;
            foreach (Peer officer in peers)
            {
                if (!officer.Ready || !officer.Pose.police || !officer.Pose.playing || officer.Pose.vehicle >= 0
                    || !Near(officer.Pose.pos, Entrance(opportunity.site), 4))
                    continue;
                officer.Credits += 325;
                SetNotice(officer, 17);
                EndOpportunity(now);
                break;
            }
        }

        private void EndOpportunity(float now)
        {
            opportunity = null;
            nextOpportunity = now + 90;
        }

        private bool Vulnerable(int site) => opportunity != null && !opportunity.falseAlarm && opportunity.site == site;

        [Serializable]
        internal sealed class CrewOrder
        {
            public int site;
            public int role;
            public bool preparing;
            public bool getaway;
            public bool handoff;
            public float remaining;
        }

        private sealed class Crew
        {
            internal Peer Collector;
            internal Peer Lookout;
            internal Peer Driver;
            internal Peer Carrier;
            internal float Ends;
            internal float AlarmAt;
            internal bool AlarmSent;
        }

        private readonly Crew[] crews = new Crew[CostaRobberies.Sites.Length];
        internal CrewOrder CrewTask { get; private set; }
        private int[] availableCrewSlots;

        private static Vector3 DriverSpot(int site) => Entrance(site) + new Vector3(7, 0, -7);

        private bool CrewAvailable(Peer peer, float now) => peer != null && peers.Contains(peer) && peer.Ready
            && peer.Pose.playing && !peer.Pose.police && now >= peer.HeldUntil && now >= peer.DoorUntil;

        private int CrewIndex(Peer peer)
        {
            for (int i = 0; i < crews.Length; i++)
                if (crews[i] != null && CrewRole(crews[i], peer) != 0)
                    return i;
            return -1;
        }

        private static int CrewRole(Crew crew, Peer peer) => crew.Collector == peer ? 1
            : crew.Lookout == peer ? 2 : crew.Driver == peer ? 3 : 0;

        private CrewOrder CrewState(Peer peer, float now)
        {
            int site = CrewIndex(peer);
            if (site < 0)
                return null;
            Crew crew = crews[site];
            bool getaway = crew.Carrier != null;
            bool preparing = !getaway && now < crew.AlarmAt;
            return new CrewOrder
            {
                site = site, role = CrewRole(crew, peer), getaway = getaway, preparing = preparing,
                handoff = getaway && crew.Driver != null && crew.Carrier == crew.Collector,
                remaining = Mathf.Max(0, (getaway ? crew.Ends : preparing ? crew.AlarmAt
                    : robberyEnds[site] - (LookoutInPlace(crew, site, now) ? 2 : 0)) - now)
            };
        }

        internal int CrewInteraction(Vector3 position, bool driving)
        {
            if (!Online || Local.police)
                return -1;
            if (CrewTask is { getaway: true, handoff: true, role: 1 } task)
                return !driving && Near(position, DriverSpot(task.site), 11) ? task.site : -1;
            if (CrewTask != null || availableCrewSlots == null)
                return -1;
            for (int i = 0; i < availableCrewSlots.Length; i++)
                if ((availableCrewSlots[i] & (driving ? 2 : 1)) != 0 && Near(position, driving ? DriverSpot(i) : Entrance(i), driving ? 7 : 3))
                    return i;
            return -1;
        }

        private void UseCrew(Peer peer, int site, float now)
        {
            if (site < 0 || site >= crews.Length || crews[site] is not Crew crew || !CrewAvailable(peer, now))
                return;
            if (crew.Carrier != null)
            {
                if (crew.Carrier == peer && crew.Collector == peer && CrewAvailable(crew.Driver, now)
                    && peer.Pose.vehicle < 0 && crew.Driver.Pose.vehicle >= 0
                    && Near(crew.Driver.Pose.pos, DriverSpot(site), 8)
                    && Near(peer.Pose.pos, crew.Driver.Pose.pos, 4)
                    && CanHandOff(peer, crew.Driver)
                    && peer.Bag.TransferLoot(site, crew.Driver.Bag))
                {
                    crew.Carrier = crew.Driver;
                    RaiseHeat(crew.Driver, now, false);
                    SetNotice(peer, 20);
                    SetNotice(crew.Driver, 20);
                }
                return;
            }
            if (CrewIndex(peer) >= 0)
                return;
            if (peer.Pose.vehicle >= 0 && crew.Driver == null && Near(peer.Pose.pos, DriverSpot(site), 7))
            {
                crew.Driver = peer;
                SetNotice(peer, 19);
            }
            else if (peer.Pose.vehicle < 0 && crew.Lookout == null && Near(peer.Pose.pos, Entrance(site), 3))
            {
                crew.Lookout = peer;
                SetNotice(peer, 18);
            }
        }

        private bool CanHandOff(Peer collector, Peer driver)
        {
            Vector3 from = collector.Pose.pos + Vector3.up * .87f;
            Vector3 to = driver.Pose.pos + Vector3.up * .87f;
            return !Physics.Linecast(from, to, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || sceneVehicles[driver.Pose.vehicle].OwnsCollider(hit.collider);
        }

        private bool LookoutInPlace(Crew crew, int site, float now) =>
            CrewAvailable(crew.Lookout, now) && crew.Lookout.Pose.vehicle < 0 && Near(crew.Lookout.Pose.pos, Entrance(site), 4);

        private void StartGetaway(int site, float now)
        {
            Crew crew = crews[site];
            if (!LookoutInPlace(crew, site, now))
                crew.Lookout = null;
            if (!CrewAvailable(crew.Driver, now) || crew.Driver.Pose.vehicle < 0
                || !Near(crew.Driver.Pose.pos, DriverSpot(site), 8))
                crew.Driver = null;
            if (crew.Lookout == null && crew.Driver == null)
            {
                crews[site] = null;
                return;
            }
            crew.Carrier = crew.Collector;
            crew.Ends = now + 90;
        }

        private void TickGetaways(float now)
        {
            for (int i = 0; i < crews.Length; i++)
            {
                Crew crew = crews[i];
                if (crew?.Carrier == null)
                    continue;
                if (now >= crew.Ends || !CrewAvailable(crew.Carrier, now))
                {
                    crews[i] = null;
                    continue;
                }
                if (crew.Carrier != crew.Driver && crew.Driver != null && !CrewAvailable(crew.Driver, now))
                    crew.Driver = null;
            }
        }

        private void FinishGetaway(Peer seller, int item, float now)
        {
            for (int i = 0; i < crews.Length; i++)
            {
                Crew crew = crews[i];
                if (crew?.Carrier != seller || !CostaBag.FromSite(i, item))
                    continue;
                foreach (Peer partner in peers)
                {
                    if (!CrewAvailable(partner, now) || (partner != crew.Collector && partner != crew.Lookout && partner != seller))
                        continue;
                    partner.Credits += 400;
                    SetNotice(partner, 21);
                }
                crews[i] = null;
                return;
            }
        }

        private bool CrewActivity(out Vector3 point, out string title)
        {
            CrewOrder task = CrewTask;
            point = default;
            title = "";
            if (task == null)
            {
                if (availableCrewSlots == null)
                    return false;
                bool driving = Local.vehicle >= 0;
                float nearest = 38 * 38;
                for (int i = 0; i < availableCrewSlots.Length; i++)
                {
                    if ((availableCrewSlots[i] & (driving ? 2 : 1)) == 0)
                        continue;
                    Vector3 spot = driving ? DriverSpot(i) : Entrance(i);
                    float distance = (Local.pos - spot).sqrMagnitude;
                    if (distance >= nearest)
                        continue;
                    nearest = distance;
                    point = spot;
                    title = driving ? L("Help as getaway driver", "Ajude como motorista de fuga")
                        : L("Help as lookout at the entrance", "Ajude como vigia na entrada");
                }
                return title.Length > 0;
            }
            if (!task.getaway)
            {
                point = task.role == 1 ? CostaRobberies.Sites[task.site].Point
                    : task.role == 2 ? Entrance(task.site) : DriverSpot(task.site);
                title = task.role == 1 ? (task.preparing
                    ? L("Collector: preparing the robbery", "Coletor: preparando o roubo")
                    : L("Collector: stay at the display", "Coletor: fique na vitrine"))
                    : task.role == 2 ? L("Lookout: cover the entrance", "Vigia: cubra a entrada")
                    : L("Driver: keep the vehicle in position", "Motorista: mantenha o veículo no ponto");
            }
            else if (task.handoff && task.role != 2)
            {
                point = DriverSpot(task.site);
                title = task.role == 1 ? L("Bring loot to the getaway driver", "Leve o saque até o motorista")
                    : L("Wait for the collector in your vehicle", "Espere o coletor no veículo");
            }
            else
            {
                Vector3 first = CostaCity.Vendors[0];
                Vector3 second = CostaCity.Vendors[1];
                point = (Local.pos - first).sqrMagnitude < (Local.pos - second).sqrMagnitude ? first : second;
                title = Local.carryingLoot ? L("Sell the crew loot", "Venda o saque da equipe")
                    : L("Crew getaway in progress", "A equipe está em fuga");
            }
            return true;
        }

        internal float ActivityTime => CrewTask?.remaining
            ?? (Opportunity != null && (Local.police
                ? Opportunity.falseAlarm && Evidence == 0 && (Alarms == null || Array.IndexOf(Alarms, true) < 0)
                : !Opportunity.falseAlarm && Bag.Count == 0) ? Opportunity.remaining : -1);
        private Dispatch dispatch;
        private float dispatchEnds;
        private float nextDispatch;

        internal Dispatch PoliceDispatch { get; private set; }
        internal int Evidence { get; private set; }

        internal static Vector3 Entrance(int site) => (site switch
        {
            0 => CostaCity.Hotel,
            1 => CostaCity.Museum,
            2 => CostaCity.Jeweler,
            _ => CostaCity.Electronics
        }) + Vector3.back * 4.5f;

        private void TickDispatch(float now)
        {
            if (dispatch != null && now >= dispatchEnds)
            {
                dispatch = null;
                nextDispatch = now + 22;
            }
            if (dispatch == null && now >= nextDispatch && CountPolice() > 0)
            {
                dispatch = new Dispatch { active = true, site = UnityEngine.Random.Range(0, robbers.Length), guard = UnityEngine.Random.value < .5f };
                dispatchEnds = now + 90;
            }
            foreach (Peer officer in peers)
            {
                if (!officer.Ready || !officer.Pose.playing || !officer.Pose.police)
                    continue;
                if (officer.Evidence > 0 && officer.Pose.vehicle < 0 && Near(officer.Pose.pos, PoliceSpawn, 5))
                {
                    officer.Credits += 600 + officer.Evidence * 125;
                    officer.Evidence = 0;
                    SetNotice(officer, 16);
                }
                if (dispatch == null || officer.Evidence > 0 || officer.Pose.vehicle >= 0
                    || (dispatch.guard && robbers[dispatch.site] != null)
                    || !Near(officer.Pose.pos, Entrance(dispatch.site), 6))
                {
                    officer.DispatchSince = -1;
                    continue;
                }
                if (officer.DispatchSince < 0)
                    officer.DispatchSince = now;
                if (now < officer.DispatchSince + (dispatch.guard ? 14 : 2))
                    continue;
                officer.Credits += dispatch.guard ? 850 : 450;
                SetNotice(officer, 15);
                dispatch = null;
                nextDispatch = now + 22;
                foreach (Peer peer in peers)
                    peer.DispatchSince = -1;
                break;
            }
        }

        internal bool TryActivity(out Vector3 point, out string title)
        {
            point = default;
            title = "";
            if (!Online)
                return false;
            if (!Local.police)
            {
                if (CrewActivity(out point, out title))
                    return true;
                if (Opportunity == null || Opportunity.falseAlarm || Bag.Count > 0)
                    return false;
                point = Entrance(Opportunity.site);
                title = L("Vulnerable store: faster collection · ", "Loja vulnerável: coleta mais rápida · ")
                    + CostaRobberies.Sites[Opportunity.site].Name;
                return true;
            }
            if (Evidence > 0)
            {
                point = PoliceSpawn;
                title = L("Deliver recovered items to the station", "Entregue os itens recuperados na delegacia");
                return true;
            }
            int alarm = Alarms == null ? -1 : Array.IndexOf(Alarms, true);
            if (alarm >= 0)
            {
                point = Entrance(alarm);
                title = L("Respond to alarm · ", "Atenda ao alarme · ") + CostaRobberies.Sites[alarm].Name;
                return true;
            }
            if (Opportunity?.falseAlarm == true)
            {
                point = Entrance(Opportunity.site);
                title = L("Check the reported alarm · ", "Verifique o alarme reportado · ") + CostaRobberies.Sites[Opportunity.site].Name;
                return true;
            }
            if (PoliceDispatch == null)
                return false;
            point = Entrance(PoliceDispatch.site);
            title = (PoliceDispatch.guard
                ? L("Protect the entrance for 14 s · ", "Proteja a entrada por 14 s · ")
                : L("Investigate the report · ", "Investigue a ocorrência · ")) + CostaRobberies.Sites[PoliceDispatch.site].Name;
            return true;
        }

        private Peer patrol;
        private int patrolRevision;

        private void TickPatrol(float now, float delta)
        {
            Peer suspect = null;
            int playing = 0;
            bool humanOfficer = false;
            foreach (Peer peer in peers)
            {
                if (!peer.Ready || !peer.Pose.playing)
                    continue;
                playing++;
                humanOfficer |= peer.Pose.police;
                int activeSite = Array.IndexOf(robbers, peer);
                if (!peer.Pose.police && peer.Pose.vehicle < 0 && now >= peer.HeldUntil
                    && (peer.Pose.wantedLevel > 0 || activeSite >= 0 && RobberyAlarm(activeSite, now))
                    && (suspect == null || peer.Pose.wantedLevel > suspect.Pose.wantedLevel))
                    suspect = peer;
            }
            if (humanOfficer || playing == 0)
            {
                patrol = null;
                return;
            }

            int site = -1;
            if (suspect != null)
            {
                float closest = 30 * 30;
                for (int i = 0; i < robbers.Length; i++)
                {
                    float distance = (suspect.Pose.pos - Entrance(i)).sqrMagnitude;
                    if (distance < closest)
                    {
                        closest = distance;
                        site = i;
                    }
                }
            }

            Vector3 response = site >= 0 ? Entrance(site) + Vector3.back * 12 : PoliceSpawn;
            if (patrol == null)
            {
                patrol = new Peer
                {
                    Pose = new Avatar
                    {
                        id = -1, name = "Patrulha NPC", police = true, playing = true, equipped = 0,
                        pos = PoliceSpawn, revision = ++patrolRevision
                    }
                };
            }
            Avatar avatar = patrol.Pose;
            if (suspect != null && site >= 0 && (avatar.pos - response).sqrMagnitude > 40 * 40 && Supported(response))
            {
                bool observed = false;
                foreach (Peer peer in peers)
                    if (peer.Ready && peer.Pose.playing
                        && ((peer.Pose.pos - response).sqrMagnitude < 18 * 18 || (peer.Pose.pos - avatar.pos).sqrMagnitude < 24 * 24))
                        observed = true;
                if (!observed)
                {
                    avatar.pos = response;
                    avatar.revision = ++patrolRevision;
                }
            }

            if (suspect != null && ArrestTarget(patrol, suspect.Pose.id, now) != null)
            {
                avatar.moving = avatar.running = false;
                BeginArrest(patrol, suspect.Pose.id, now);
                AdvanceArrest(patrol, now);
                return;
            }
            patrol.ArrestTarget = 0;
            avatar.arrestProgress = 0;

            Vector3 destination = suspect == null ? PoliceSpawn
                : ClearArrestLine(avatar.pos, suspect.Pose.pos) || site < 0 ? suspect.Pose.pos : Entrance(site);
            Vector3 desired = destination - avatar.pos;
            desired.y = 0;
            avatar.moving = avatar.running = false;
            if (desired.sqrMagnitude < .25f || delta <= 0)
                return;
            float angle = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
            float travel = Mathf.Min(desired.magnitude, Mathf.Min(delta, .1f) * (suspect == null ? 2.1f : 4.3f));
            for (int i = 0; i < 5; i++)
            {
                float offset = i == 0 ? 0 : ((i + 1) / 2) * 34 * (i % 2 == 0 ? -1 : 1);
                Vector3 direction = Quaternion.Euler(0, angle + offset, 0) * Vector3.forward;
                Vector3 next = avatar.pos + direction * travel;
                if (!Supported(next) || Physics.SphereCast(avatar.pos + Vector3.up * .86f, .34f,
                    direction, out _, Mathf.Max(travel, .6f), Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    continue;
                avatar.pos = next;
                avatar.yaw = angle + offset;
                avatar.moving = true;
                avatar.running = suspect != null;
                break;
            }
        }

        private void ResetActivities()
        {
            dispatch = PoliceDispatch = null;
            patrol = null;
            patrolRevision = 0;
            nextDispatch = dispatchEnds = 0;
            Evidence = 0;
            Array.Clear(crews, 0, crews.Length);
            CrewTask = null;
            availableCrewSlots = null;
            opportunity = Opportunity = null;
            nextOpportunity = opportunityEnds = 0;
            nextFalseAlarm = true;
        }
    }
}
