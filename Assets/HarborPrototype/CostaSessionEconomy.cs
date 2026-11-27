using System;
using System.IO;
using UnityEngine;

namespace CostaLivre
{
    internal sealed partial class CostaSession
    {
        internal static readonly Vector3[] Terminals =
        {
            CostaCity.Station + new Vector3(-3,0,-5),
            CostaCity.Hotel + new Vector3(4,0,-5),
            CostaCity.Electronics + new Vector3(4,0,-5),
            new(14,0,CostaCity.Districts[0].Center + 10),
            new(14,0,CostaCity.Districts[1].Center + 10),
            new(14,0,CostaCity.Districts[2].Center + 10)
        };

        internal static bool AtBank(Vector3 point)
        {
            foreach (Vector3 terminal in Terminals)
                if (Near(point,terminal,2.1f)) return true;
            return false;
        }

        [Serializable]
        private sealed class AccountUpdate
        {
            public string type = "account";
            public int protocol = Protocol;
            public CostaLedger.Entry[] bankHistory;
            public CostaCampaign.Progress campaign;
        }

        internal int StaminaRefill { get; private set; }

        private CostaLedger ledger;
        private int nextTransaction;
        internal int BankPersonal { get; private set; }
        internal int BankBusiness { get; private set; }
        internal CostaLedger.Entry[] BankHistory { get; private set; }
        internal void BankRequest(CostaLedger.Operation op, int amount, int recipient)
        {
            Send(new Frame { type = "bank", index = (int)op, amount = amount, id = recipient,
                transaction = ++nextTransaction });
        }

        internal bool TryBankRecipient(int index,out RosterEntry result,out int count)
        {
            result = null;
            count = 0;
            foreach (RosterEntry a in bankRoster)
            {
                if (a.id <= 0 || a.id == Local.id) continue;
                if (count == index || result == null) result = a;
                count++;
            }
            return count > 0;
        }

        private void ReceiveBank(Peer peer, Frame msg)
        {
            if (msg.index < 0 || msg.index > (int)CostaLedger.Operation.Transfer || msg.transaction <= 0
                || peer.Pose.vehicle >= 0 || !AtBank(peer.Pose.pos))
                return;
            Peer recipient = msg.index == (int)CostaLedger.Operation.Transfer
                ? peers.Find(other => other.Ready && other.Pose.id == msg.id) : null;
            bool accepted = ledger.BeginBank(peer.Account,(CostaLedger.Operation)msg.index,msg.amount,msg.transaction,
                recipient?.Account, success =>
                {
                    SetNotice(peer,success ? 36 : 37);
                    if (success && recipient != null)
                        SetNotice(recipient,38);
                });
            if (!accepted)
                SetNotice(peer,ledger.BankWritePending ? 41 : 37);
        }

        internal CostaCampaign Campaign { get; set; }
        internal CostaCampaign.Progress Narrative { get; private set; }

        internal void CampaignRequest(string checkpoint,int choice) =>
            Send(new Frame { type = "campaign", name = checkpoint, index = choice, transaction = ++nextTransaction });

        private void ReceiveCampaign(Peer peer,Frame msg)
        {
            if (Campaign == null || msg.transaction <= 0 || string.IsNullOrEmpty(msg.name)) return;
            bool success = Campaign.TryAdvance(ledger,peer.Account,msg.index,msg.name,msg.transaction,
                peer.Pose.pos,peer.Pose.vehicle >= 0,peer.Bag);
            SetNotice(peer,success ? 39 : 37);
        }

        internal void WorkshopRequest(int vehicle,CostaBuildTuning.Part part,int value,bool bank) =>
            Send(new Frame { type = "workshop", id = vehicle, index = (int)part, option = value, amount = bank ? 1 : 0,
                transaction = ++nextTransaction });

        private void ReceiveWorkshop(Peer peer,Frame msg)
        {
            int i = msg.id;
            var part = (CostaBuildTuning.Part)msg.index;
            if (i < 0 || i >= sceneVehicles.Length || i != peer.Pose.vehicle || msg.transaction <= 0
                || msg.option < 0 || msg.option > 0xffffff || !sceneVehicles[i].Customizable
                || !CostaBuildTuning.AtWorkshop(peer.Pose.pos) || Mathf.Abs(hostCars[i].speed) > .52f
                || !CostaBuildTuning.Allowed(part,(int)msg.option,sceneVehicles[i].IsBike))
                return;
            long previous = hostCars[i].tune;
            int next = (int)msg.option;
            long changed = CostaBuildTuning.Change(previous,part,next);
            if (!CanTune(previous,part,next,peer.Account) || changed == previous ||
                !ledger.Apply(peer.Account,CostaLedger.Operation.Workshop,CostaBuildTuning.Price(part,next),msg.transaction,
                    vehicle:i,build:changed,fromBank:msg.amount == 1))
            {
                SetNotice(peer,37);
                return;
            }
            hostCars[i].tune = changed;
            sceneVehicles[i].ApplyBuild(changed);
            SetNotice(peer,36);
        }

        internal static bool CanTune(long current,CostaBuildTuning.Part part,int next,CostaLedger.Account account) =>
            part < CostaBuildTuning.Part.Engine || part > CostaBuildTuning.Part.Grip
            || next <= CostaBuildTuning.Option(current,part)+1
            || ((account?.campaign?.flags ?? 0) & 128) != 0;

        private void TickPayroll(float now)
        {
            if (ledger.BankWritePending) return;
            foreach (Peer peer in peers)
            {
                if(!peer.Ready || !peer.Pose.playing || !peer.Pose.police || peer.NextPayroll<=0 || now<peer.NextPayroll)
                    continue;
                peer.NextPayroll=now+300;
                if(ledger.Apply(peer.Account,CostaLedger.Operation.Payroll,250))
                    SetNotice(peer,40);
            }
        }

        private bool Reward(Peer peer, int amount) => amount > 0 && ledger.Apply(peer.Account,CostaLedger.Operation.Reward,amount);
        private bool Purchase(Peer peer,int amount,int weapon) => ledger.Apply(peer.Account,CostaLedger.Operation.Purchase,amount,weapon:weapon);
    }
}
