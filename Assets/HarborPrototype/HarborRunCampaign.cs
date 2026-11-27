using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    public sealed partial class HarborRun
    {
        private CostaCampaign.Progress CampaignProgress => session.Online ? session.Narrative : jobs.Account.campaign;

        private bool CanOpenCampaign() => session.Campaign != null
            && session.Campaign.Checkpoint(CampaignProgress,player.transform.position,car != null,CurrentBag);

        private void DrawCampaign()
        {
            var campaign = session.Campaign;
            if (campaign == null) return;
            var progress = CampaignProgress;
            var node = campaign.Current(progress);
            DrawServiceFrame(L("COASTAL RECORD · JOURNAL","REGISTRO DO LITORAL · DIÁRIO"),474);
            GUI.Label(new Rect(18,51,570,25),$"{L("Act","Ato")} {node.act}  ·  {node.speaker}   ·   "
                +$"{L("Trust","Confiança")}  Lia {progress?.journalist ?? 0}   Rui {progress?.mechanic ?? 0}   Célia {progress?.inspector ?? 0}");
            var prose = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            GUI.Label(new Rect(18,89,555,75),node.Title,prose);
            if (node.ending == 0)
            {
                bool atCheckpoint = CanOpenCampaign();
                var options = node.choices;
                for (int i=0;i<options.Length;i++)
                {
                    GUI.enabled = atCheckpoint && CostaCampaign.Available(options[i],progress);
                    if (CostaUi.Button(new Rect(18,160+i*40,577,35),options[i].Caption,serviceButton))
                    {
                        if (session.Online)
                            session.CampaignRequest(node.id,i);
                        else
                            campaign.TryAdvance(jobs.Ledger,jobs.Account,i,node.id,0,player.transform.position,car != null,CurrentBag);
                    }
                    GUI.enabled = true;
                }
            }
            GUI.Label(new Rect(18,297,577,25),L("Decision history","Histórico de decisões"));
            var decisions = progress?.decisions;
            if (decisions != null)
                for (int i=0;i<6 && i<decisions.Length;i++)
                    GUI.Label(new Rect(18,327+i*20,577,21),"· "+campaign.Caption(decisions[decisions.Length-1-i]));
            EndServiceFrame();
        }
    }
}
