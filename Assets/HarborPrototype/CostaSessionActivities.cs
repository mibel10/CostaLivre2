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
            public int site;
            public bool guard;
        }

        [Serializable]
        internal sealed class CityOpportunity
        {
            public int site;
            public bool falseAlarm;
        }

        private CityOpportunity opportunity;
        private float opportunityEnds;
        private float nextOpportunity;
        private bool nextFalseAlarm = true;
        internal CityOpportunity Opportunity { get; private set; }

        private void TickOpportunity(float now)
        {
            if (opportunity != null && now >= opportunityEnds)
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
                        opportunity = new CityOpportunity { site = site, falseAlarm = nextFalseAlarm };
                        opportunityEnds = now + 75;
                        nextFalseAlarm = !nextFalseAlarm;
                        break;
                    }
                    nextOpportunity = now + 8;
                }
            }
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
                dispatch = new Dispatch { site = UnityEngine.Random.Range(0, robbers.Length), guard = UnityEngine.Random.value < .5f };
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

        private void ResetDispatch()
        {
            dispatch = PoliceDispatch = null;
            nextDispatch = dispatchEnds = 0;
            Evidence = 0;
            opportunity = Opportunity = null;
            nextOpportunity = opportunityEnds = 0;
            nextFalseAlarm = true;
        }
    }
}
