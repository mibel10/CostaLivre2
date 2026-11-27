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
            if (!Online || !Local.police)
                return false;
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
        }
    }
}
