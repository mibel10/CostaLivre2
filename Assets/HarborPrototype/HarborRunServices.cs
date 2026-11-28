using System;
using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    public sealed partial class HarborRun
    {
        private int bankingAmount = 250;
        private int bankingRecipient;
        private string bankingMessage;
        private GUIStyle serviceButton;
        private Matrix4x4 savedServiceMatrix;

        private Rect DrawServiceFrame(string title, float frameHeight)
        {
            float scale = Mathf.Min(Screen.width / 650f,Screen.height / frameHeight,1.5f);
            savedServiceMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            Rect frame = new((Screen.width/scale-616)*.5f,(Screen.height/scale-frameHeight)*.5f,616,frameHeight);
            Color old = GUI.color;
            GUI.color = new Color(.055f,.085f,.093f,.98f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = new Color(.28f,.75f,.67f);
            GUI.DrawTexture(new Rect(frame.x,frame.y,frame.width,3),Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(frame.x+18,frame.y+12,448,29),title);
            serviceButton ??= new GUIStyle(GUI.skin.button) { fontSize = 15 };
            GUI.BeginGroup(frame);
            if (CostaUi.Button(new Rect(500,10,102,30),L("Close","Fechar"),serviceButton))
                CloseOverlay();
            return frame;
        }

        private void EndServiceFrame()
        {
            GUI.EndGroup();
            GUI.matrix = savedServiceMatrix;
        }

        private CostaLedger.Entry[] drawnBankHistory;
        private bool drawnBankLanguage;
        private readonly string[] bankRows = new string[7];

        private void CacheBankRows(CostaLedger.Entry[] history)
        {
            if (ReferenceEquals(history,drawnBankHistory) && drawnBankLanguage == CostaText.Portuguese)
                return;
            drawnBankHistory = history;
            drawnBankLanguage = CostaText.Portuguese;
            Array.Clear(bankRows,0,bankRows.Length);
            if (history == null)
                return;
            for (int row = 0; row < bankRows.Length && row < history.Length; row++)
            {
                var entry = history[history.Length-1-row];
                if (entry == null || string.IsNullOrEmpty(entry.id) || entry.utc is < 0 or > 253402300799L)
                    continue;
                bankRows[row] = $"{DateTimeOffset.FromUnixTimeSeconds(entry.utc).ToLocalTime():HH:mm}  ·  "
                    + BankLabel(entry.operation) + $"  ·  ${entry.amount:N0}  ·  {entry.id.Substring(0,Math.Min(7,entry.id.Length))}";
            }
        }

        private void DrawBank()
        {
            DrawServiceFrame(L("COASTAL BANK","BANCO COSTEIRO"),434);
            bool online = session.Online;
            var account = online ? null : jobs.Account;
            int personal = online ? session.BankPersonal : account.personal;
            int business = online ? session.BankBusiness : account.business;
            GUI.Label(new Rect(18,54,575,24), L("CASH","ESPÉCIE") + $"  ${CurrentCash:N0}     " +
                L("PERSONAL","PESSOAL")+$"  ${personal:N0}     "+L("BUSINESS","EMPRESARIAL")+$"  ${business:N0}");
            if (CostaUi.Button(new Rect(18,88,68,31),"− 250",serviceButton))
                bankingAmount = Mathf.Max(1, bankingAmount - 250);
            if (CostaUi.Button(new Rect(91,88,55,31),"− 1",serviceButton))
                bankingAmount = Mathf.Max(1, bankingAmount - 1);
            GUI.Label(new Rect(158,90,111,30),$"${bankingAmount:N0}");
            if (CostaUi.Button(new Rect(272,88,55,31),"+ 1",serviceButton))
                bankingAmount = Mathf.Min(CostaLedger.Limit, bankingAmount + 1);
            if (CostaUi.Button(new Rect(333,88,75,31),"+ 250",serviceButton))
                bankingAmount = Mathf.Min(CostaLedger.Limit, bankingAmount + 250);
            if (CostaUi.Button(new Rect(414,88,89,31),"+ 1.000",serviceButton))
                bankingAmount = Mathf.Min(CostaLedger.Limit, bankingAmount + 1000);

            if (CostaUi.Button(new Rect(18,132,137,34),L("Deposit","Depositar"),serviceButton))
                BankAction(CostaLedger.Operation.Deposit);
            if (CostaUi.Button(new Rect(164,132,137,34),L("Withdraw","Sacar"),serviceButton))
                BankAction(CostaLedger.Operation.Withdraw);
            if (CostaUi.Button(new Rect(310,132,137,34),L("To business","P/ empresa"),serviceButton))
                BankAction(CostaLedger.Operation.ToBusiness);
            if (CostaUi.Button(new Rect(456,132,137,34),L("To personal","P/ pessoal"),serviceButton))
                BankAction(CostaLedger.Operation.ToPersonal);
            if (online && session.TryBankRecipient(bankingRecipient, out var recipient, out int count))
            {
                if (CostaUi.Button(new Rect(18,178,290,30),recipient.name+"   ▸",serviceButton))
                    bankingRecipient = (bankingRecipient+1)%count;
                if (CostaUi.Button(new Rect(319,178,274,30),L("Send from personal","Transferir da conta pessoal"),serviceButton))
                    BankAction(CostaLedger.Operation.Transfer,recipient.id);
            }
            if (!online && jobs.DailyPropertyCost > 0)
            {
                string automatic = account.autoProperty ? L("Auto-pay on","Débito automático ligado")
                    : L("Auto-pay off","Débito automático desligado");
                if (CostaUi.Button(new Rect(18,178,575,30),automatic+$" · ${jobs.DailyPropertyCost}/{L("day","dia")}",serviceButton))
                    if (!jobs.Ledger.PropertyBilling(account,0,CostaJobs.BankDay,!account.autoProperty))
                        bankingMessage = L("Transaction declined","Transação recusada");
            }
            GUI.Label(new Rect(18,214,575,26), online ? session.CurrentNotice : bankingMessage ?? "");
            var history = online ? session.BankHistory : account.history;
            GUI.Label(new Rect(18,245,575,24),L("Recent transactions","Transações recentes"));
            CacheBankRows(history);
            for (int row = 0; row < bankRows.Length && bankRows[row] != null; row++)
                GUI.Label(new Rect(18,274+row*20,575,20),bankRows[row]);
            EndServiceFrame();
        }

        private static string BankLabel(CostaLedger.Operation op) => op switch
        {
            CostaLedger.Operation.Deposit => L("Deposit","Depósito"),
            CostaLedger.Operation.Withdraw => L("Withdrawal","Saque"),
            CostaLedger.Operation.Transfer => L("Transfer","Transferência"),
            CostaLedger.Operation.ToBusiness => L("To business","Para empresa"),
            CostaLedger.Operation.ToPersonal => L("To personal","Para conta pessoal"),
            CostaLedger.Operation.Reward => L("Reward","Recompensa"),
            CostaLedger.Operation.Payroll => L("Salary","Salário"),
            CostaLedger.Operation.PropertyFee => L("Property fee","Taxa de imóvel"),
            CostaLedger.Operation.Workshop => L("Workshop","Oficina"),
            _ => L("Purchase","Compra")
        };

        private void BankAction(CostaLedger.Operation op,int recipient = 0)
        {
            if (session.Online)
            {
                session.BankRequest(op,bankingAmount,recipient);
                return;
            }
            if (jobs.Ledger.BankWritePending)
                return;
            bool accepted = jobs.Ledger.BeginBank(jobs.Account,op,bankingAmount,0,null,success =>
                bankingMessage = success ? L("Transaction complete","Transação concluída")
                    : L("Transaction declined","Transação recusada"));
            bankingMessage = accepted ? L("Saving transaction…","Salvando transação…")
                : L("Transaction declined","Transação recusada");
        }

        private float nextPropertyCheck;
        private void UpdatePropertyBilling()
        {
            jobs.Ledger.FinishBank();
            float now = Time.unscaledTime;
            if (now<nextPropertyCheck || session.Online || jobs.Ledger.BankWritePending) return;
            nextPropertyCheck=now+30;
            if(jobs.DailyPropertyCost>0 && jobs.Ledger.PropertyBilling(jobs.Account,jobs.DailyPropertyCost,CostaJobs.BankDay))
                bankingMessage=L("Property fee paid","Taxa de imóvel paga");
        }

        private void BuildAtms()
        {
            foreach (Vector3 p in CostaSession.Terminals)
            {
                Color shell = new(.15f,.29f,.31f);
                harbor.Box("ATM pedestal",p+new Vector3(0,.60f,.78f),new(.81f,1.20f,.49f),shell,true);
                harbor.Box("ATM screen",p+new Vector3(0,1.13f,.51f),new(.51f,.33f,.057f),new(.30f,.61f,.59f));
                harbor.Box("ATM keypad",p+new Vector3(0,.86f,.47f),new(.23f,.082f,.11f),new(.52f,.54f,.55f));
                harbor.Sign("BANCO",p+new Vector3(0,1.47f,.70f),.087f,new(.83f,.93f,.83f));
            }
        }
    }
}
