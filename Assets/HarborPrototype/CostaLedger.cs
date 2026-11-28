using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaLedger
    {
        internal const int Limit = 1000000000;
        internal enum Operation { Deposit, Withdraw, ToBusiness, ToPersonal, Transfer, Reward, Purchase, Workshop, Payroll, PropertyFee }
        [Serializable] internal sealed class Entry
        {
            public string id;
            public long utc;
            public int amount;
            public Operation operation;
        }

        [Serializable] internal sealed class Account
        {
            public string key;
            public int cash = 4500;
            public int personal;
            public int business;
            public int serial;
            public long nextPropertyFee;
            public bool autoProperty;
            public int weapons;
            public int properties;
            public int completedJobs;
            public int[] soloBag;
            public bool soloMigrated;
            public int relics;
            public Entry[] history = Array.Empty<Entry>();
            public CostaCampaign.Progress campaign;
            public CostaCheckpoint checkpoint;
            public long[] mods = new long[16];
        }

        [Serializable] private sealed class Book
        {
            public int version = 1;
            public Account[] accounts = Array.Empty<Account>();
        }

        private readonly string file;
        private readonly object gate = new();
        private Book book;
        private bool available;
        private bool recovered;
        private Task<bool> bankWrite;
        private byte[] stagedBytes;
        private bool staging;
        private Action<bool> publishBank;
        internal bool BankWritePending => bankWrite != null;

        internal CostaLedger(string path)
        {
            file = path;
            available = TryRead(path, out book);
            if ((!available || book == null) && File.Exists(path + ".old") && TryRead(path + ".old", out Book backup) && backup != null)
            {
                book = backup;
                available = recovered = true;
            }
            if (!available)
                book = null;
            book ??= new Book();
        }

        private static bool TryRead(string path, out Book result)
        {
            result = null;
            try
            {
                if (!File.Exists(path))
                    return true;
                result = JsonUtility.FromJson<Book>(File.ReadAllText(path));
                if (result?.version != 1 || result.accounts == null)
                    return false;
                var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (Account a in result.accounts)
                    if (a == null || string.IsNullOrEmpty(a.key) || a.cash is < 0 or > Limit
                        || a.personal is < 0 or > Limit || a.business is < 0 or > Limit
                        || a.mods?.Length != 16 || a.history == null || a.history.Length > 12 || a.serial < 0 || !keys.Add(a.key)
                        || Array.Exists(a.history, entry => entry == null || string.IsNullOrEmpty(entry.id)
                            || entry.utc < 0 || entry.utc > 253402300799L || !Enum.IsDefined(typeof(Operation), entry.operation))
                        || a.soloMigrated && (a.soloBag == null || a.soloBag.Length != CostaBag.Catalog.Length))
                        return false;
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                return false;
            }
        }

        internal Account Get(string key, int initial = 4500)
        {
            lock (gate)
            {
                Account a = Array.Find(book.accounts, item => item.key == key);
                if (a != null)
                    return a;
                a = new Account { key = key, cash = Mathf.Clamp(initial, 0, Limit) };
                Array.Resize(ref book.accounts, book.accounts.Length + 1);
                book.accounts[^1] = a;
                return a;
            }
        }

        internal void MigrateSolo(Account account, int weapons, int properties, int completed, int[] bag)
        {
            lock (gate)
            {
                if (account.soloMigrated)
                    return;
                account.weapons = weapons;
                account.properties = properties;
                account.completedJobs = completed;
                account.soloBag = (int[])bag.Clone();
                account.soloMigrated = true;
                // The next successful transaction persists this migration with its data.
            }
        }

        internal bool SaveSolo(Account account, int cashDelta = 0, int property = 0,
            int completed = 0, int[] bag = null)
        {
            lock (gate)
            {
                long cash = (long)account.cash + cashDelta;
                if (!available || bankWrite != null || !account.soloMigrated || cash is < 0 or > Limit
                    || (account.properties & property) != 0 || (account.completedJobs & completed) != 0)
                    return false;
                var previous = (account.cash, account.properties, account.completedJobs, account.soloBag, account.history);
                account.cash = (int)cash;
                account.properties |= property;
                account.completedJobs |= completed;
                if (bag != null)
                    account.soloBag = (int[])bag.Clone();
                if (cashDelta != 0)
                    Append(account, cashDelta > 0 ? Operation.Reward : Operation.Purchase, cashDelta, Guid.NewGuid().ToString("N"));
                if (Write())
                    return true;
                (account.cash, account.properties, account.completedJobs, account.soloBag, account.history) = previous;
                return false;
            }
        }

        // Prepare on the simulation thread. Only immutable bytes reach the I/O worker.
        internal bool BeginBank(Account account, Operation operation, int amount, int serial,
            Account target, Action<bool> completion)
        {
            lock (gate)
            {
                if (bankWrite != null || operation is < Operation.Deposit or > Operation.Transfer)
                    return false;

                var before = new BalanceState(account);
                BalanceState targetBefore = target == null ? default : new BalanceState(target);
                staging = true;
                bool accepted;
                try { accepted = Apply(account,operation,amount,serial,target); }
                finally { staging = false; }
                if (!accepted)
                    return false;
                var after = new BalanceState(account);
                BalanceState targetAfter = target == null ? default : new BalanceState(target);
                before.Restore(account);
                if (target != null) targetBefore.Restore(target);
                byte[] bytes = stagedBytes;
                stagedBytes = null;
                bool preserveBackup = recovered;
                bankWrite = Task.Run(() => WriteBytes(bytes,preserveBackup));
                publishBank = success =>
                {
                    if (success)
                    {
                        after.Restore(account);
                        if (target != null) targetAfter.Restore(target);
                        recovered = false;
                    }
                    completion(success);
                };
                return true;
            }
        }

        internal void FinishBank(bool wait = false)
        {
            lock (gate)
            {
                if (bankWrite == null || !wait && !bankWrite.IsCompleted)
                    return;
                bool success = bankWrite.GetAwaiter().GetResult();
                bankWrite = null;
                Action<bool> publish = publishBank;
                publishBank = null;
                publish(success);
            }
        }

        private readonly struct BalanceState
        {
            private readonly int cash, personal, business, serial;
            private readonly Entry[] history;
            internal BalanceState(Account account)
            {
                cash = account.cash; personal = account.personal; business = account.business;
                serial = account.serial; history = account.history;
            }
            internal void Restore(Account account)
            {
                account.cash = cash; account.personal = personal; account.business = business;
                account.serial = serial; account.history = history;
            }
        }

        internal bool Apply(Account account, Operation op, int amount, int serial = 0, Account target = null,
            int vehicle = -1, long build = 0, bool fromBank = false, int weapon = -1, int relic = -1)
        {
            lock (gate)
            {
                if (!available || bankWrite != null || amount <= 0 || amount > Limit || serial < 0
                    || serial != 0 && serial <= account.serial || !Array.Exists(book.accounts,a => a == account))
                    return false;
                if (relic >= 0 && (op != Operation.Reward || relic >= 12 || (account.relics & (1 << relic)) != 0))
                    return false;
                int cash = account.cash, personal = account.personal, business = account.business;
                switch (op)
                {
                    case Operation.Deposit: cash -= amount; personal += amount; break;
                    case Operation.Withdraw: personal -= amount; cash += amount; break;
                    case Operation.ToBusiness: personal -= amount; business += amount; break;
                    case Operation.ToPersonal: business -= amount; personal += amount; break;
                    case Operation.Transfer:
                        if (target == null || target == account || target.personal > Limit - amount
                            || !Array.Exists(book.accounts,a => a == target))
                            return false;
                        personal -= amount; break;
                    case Operation.Reward: cash += amount; break;
                    case Operation.Payroll: personal += amount; break;
                    case Operation.Workshop:
                        if (vehicle < 0 || vehicle >= account.mods.Length || build < 0)
                            return false;
                        if (fromBank) personal -= amount; else cash -= amount;
                        break;
                    case Operation.Purchase:
                        if (weapon >= 31 || weapon >= 0 && (account.weapons & (1 << weapon)) != 0) return false;
                        cash -= amount;
                        break;
                    default: return false;
                }
                if (cash is < 0 or > Limit || personal is < 0 or > Limit || business is < 0 or > Limit)
                    return false;
                var prev = (account.cash, account.personal, account.business, account.serial, account.history, account.weapons, account.relics);
                long oldMod = op == Operation.Workshop ? account.mods[vehicle] : 0;
                Entry[] targetHistory = target?.history;
                string id = Guid.NewGuid().ToString("N");
                account.cash = cash; account.personal = personal; account.business = business;
                if (serial != 0)
                    account.serial = serial;
                Append(account, op, op is Operation.Transfer or Operation.Purchase or Operation.Workshop ? -amount : amount, id);
                if (op == Operation.Workshop)
                    account.mods[vehicle] = build;
                if (op == Operation.Purchase && weapon >= 0)
                    account.weapons |= 1 << weapon;
                if (relic >= 0)
                    account.relics |= 1 << relic;
                if (op == Operation.Transfer)
                {
                    target.personal += amount;
                    Append(target, op, amount, id);
                }
                if (Write())
                    return true;
                (account.cash, account.personal, account.business, account.serial, account.history, account.weapons, account.relics) = prev;
                if (op == Operation.Workshop)
                    account.mods[vehicle] = oldMod;
                if (op == Operation.Transfer)
                {
                    target.personal -= amount;
                    target.history = targetHistory;
                }
                return false;
            }
        }

        internal bool PropertyBilling(Account account, int amount, long day, bool? automatic = null)
        {
            lock(gate)
            {
                if (!available || bankWrite != null || !Array.Exists(book.accounts,a => a == account)) return false;
                var prior = (account.autoProperty, account.nextPropertyFee,account.personal,account.history);
                if (automatic.HasValue)
                {
                    if (automatic.Value == account.autoProperty) return true;
                    account.autoProperty=automatic.Value;
                    if (account.autoProperty && account.nextPropertyFee==0) account.nextPropertyFee=day+1;
                }
                else
                {
                    if(!account.autoProperty || amount<=0 || day<account.nextPropertyFee || account.personal<amount)
                        return false;
                    account.personal-=amount;
                    account.nextPropertyFee=day+1;
                    Append(account,Operation.PropertyFee,-amount,Guid.NewGuid().ToString("N"));
                }
                if(Write()) return true;
                (account.autoProperty,account.nextPropertyFee,account.personal,account.history)=prior;
                return false;
            }
        }

        internal bool SaveCampaign(Account account,string expected,int serial,CostaCampaign.Progress progress,int reward,CostaCheckpoint checkpoint = null)
        {
            lock (gate)
            {
                if (!available || bankWrite != null || account.campaign?.node != expected || serial < 0 || serial != 0 && serial <= account.serial
                    || !Array.Exists(book.accounts,a => a == account) || reward < 0 || account.cash > Limit-reward
                    || checkpoint != null && !checkpoint.Valid(progress?.node))
                    return false;
                var previous = (account.campaign, account.cash, account.serial, account.history, account.checkpoint);
                account.campaign = progress;
                if (checkpoint != null) account.checkpoint = checkpoint;
                account.cash += reward;
                if (serial != 0) account.serial = serial;
                if (reward > 0) Append(account,Operation.Reward,reward,Guid.NewGuid().ToString("N"));
                if (Write()) return true;
                (account.campaign, account.cash, account.serial, account.history, account.checkpoint) = previous;
                return false;
            }
        }

        internal bool SaveCheckpoint(Account account, CostaCheckpoint checkpoint)
        {
            lock (gate)
            {
                if (!available || bankWrite != null || checkpoint == null ||
                    !checkpoint.Valid(account.campaign?.node ?? checkpoint.node) || !Array.Exists(book.accounts, a => a == account))
                    return false;
                CostaCheckpoint prior = account.checkpoint;
                account.checkpoint = checkpoint;
                if (Write())
                    return true;
                account.checkpoint = prior;
                return false;
            }
        }

        private static void Append(Account a, Operation op, int amount, string id)
        {
            int copy = Math.Min(a.history.Length, 11);
            var result = new Entry[copy+1];
            Array.Copy(a.history, Math.Max(0,a.history.Length-copy), result, 0, copy);
            result[^1] = new Entry { operation = op, amount = amount, id = id, utc = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            a.history = result;
        }

        private bool Write()
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(book));
            if (staging)
            {
                stagedBytes = bytes;
                return true;
            }
            if (!WriteBytes(bytes,recovered))
                return false;
            recovered = false;
            return true;
        }

        private bool WriteBytes(byte[] bytes, bool preserveBackup)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                using (var stream = new FileStream(file + ".new", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(file))
                    File.Replace(file + ".new", file, preserveBackup ? null : file + ".old");
                else
                    File.Move(file + ".new", file);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Debug.LogWarning("Costa Ledger could not save: " + ex.GetType().Name);
                return false;
            }
        }
    }
}
