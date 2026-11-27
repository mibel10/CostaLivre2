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

        internal bool TryBankRecipient(int index,out Avatar result,out int count)
        {
            result = null;
            count = 0;
            foreach (Avatar a in Players)
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
            bool success = ledger.Apply(peer.Account,(CostaLedger.Operation)msg.index,msg.amount,msg.transaction,recipient?.Account);
            SetNotice(peer,success ? 36 : 37);
            if (success && recipient != null)
                SetNotice(recipient,38);
        }

        private bool Reward(Peer peer, int amount) => amount > 0 && ledger.Apply(peer.Account,CostaLedger.Operation.Reward,amount);
        private bool Purchase(Peer peer, int amount) => ledger.Apply(peer.Account,CostaLedger.Operation.Purchase,amount);
    }
}
