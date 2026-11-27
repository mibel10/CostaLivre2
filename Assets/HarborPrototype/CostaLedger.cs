using System;
using System.IO;
using UnityEngine;

namespace CostaLivre
{
    internal sealed class CostaLedger
    {
        internal const int Limit = 1000000000;
        internal enum Operation { Deposit, Withdraw, ToBusiness, ToPersonal, Transfer, Reward, Purchase, Workshop }
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
            public Entry[] history = Array.Empty<Entry>();
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
        internal CostaLedger(string path)
        {
            file = path;
            available = TryRead(path, out book);
            if ((!available || book == null) && File.Exists(path + ".old") && TryRead(path + ".old", out book))
                available = true;
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
                foreach (Account a in result.accounts)
                    if (a == null || string.IsNullOrEmpty(a.key) || a.cash is < 0 or > Limit
                        || a.personal is < 0 or > Limit || a.business is < 0 or > Limit
                        || a.mods?.Length != 16 || a.history == null || a.serial < 0)
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

        internal bool Apply(Account account, Operation op, int amount, int serial = 0, Account target = null,
            int vehicle = -1, long build = 0, bool fromBank = false)
        {
            lock (gate)
            {
                if (!available || amount <= 0 || amount > Limit || serial < 0
                    || serial != 0 && serial <= account.serial || !Array.Exists(book.accounts,a => a == account))
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
                    case Operation.Workshop:
                        if (vehicle < 0 || vehicle >= account.mods.Length || build < 0)
                            return false;
                        if (fromBank) personal -= amount; else cash -= amount;
                        break;
                    case Operation.Purchase: cash -= amount; break;
                    default: return false;
                }
                if (cash is < 0 or > Limit || personal is < 0 or > Limit || business is < 0 or > Limit)
                    return false;
                var prev = (account.cash, account.personal, account.business, account.serial, account.history);
                long oldMod = op == Operation.Workshop ? account.mods[vehicle] : 0;
                Entry[] targetHistory = target?.history;
                string id = Guid.NewGuid().ToString("N");
                account.cash = cash; account.personal = personal; account.business = business;
                if (serial != 0)
                    account.serial = serial;
                Append(account, op, op is Operation.Transfer or Operation.Purchase or Operation.Workshop ? -amount : amount, id);
                if (op == Operation.Workshop)
                    account.mods[vehicle] = build;
                if (op == Operation.Transfer)
                {
                    target.personal += amount;
                    Append(target, op, amount, id);
                }
                if (Write())
                    return true;
                (account.cash, account.personal, account.business, account.serial, account.history) = prev;
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
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                using (var stream = new FileStream(file + ".new", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(book));
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(file))
                    File.Replace(file + ".new", file, file + ".old");
                else
                    File.Move(file + ".new", file);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning("Costa Ledger could not save: " + ex.GetType().Name);
                return false;
            }
        }
    }
}
