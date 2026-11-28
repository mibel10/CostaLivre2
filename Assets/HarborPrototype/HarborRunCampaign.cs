using UnityEngine;
using static CostaLivre.CostaText;

namespace CostaLivre
{
    public sealed partial class HarborRun
    {
        private GUIStyle campaignProse;
        private CostaCampaign.Progress CampaignProgress => session.Online ? session.Narrative : jobs.Account.campaign;

        private bool CanOpenCampaign() => session.Campaign != null
            && session.Campaign.Checkpoint(CampaignProgress,player.transform.position,car != null,CurrentBag,car?.Speed ?? 0);

        private void DrawCampaign()
        {
            var campaign = session.Campaign;
            if (campaign == null) return;
            var progress = CampaignProgress;
            var node = campaign.Current(progress);
            DrawServiceFrame(L("COASTAL RECORD · JOURNAL","REGISTRO DO LITORAL · DIÁRIO"),474);
            GUI.Label(new Rect(18,51,570,25),$"{L("Act","Ato")} {node.act}  ·  {node.speaker}   ·   "
                +$"{L("Trust","Confiança")}  Lia {progress?.journalist ?? 0}   Rui {progress?.mechanic ?? 0}   Célia {progress?.inspector ?? 0}");
            campaignProse ??= new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 16 };
            GUI.Label(new Rect(18,89,555,75),CostaCampaign.Dialogue(node, progress),campaignProse);
            if (node.ending == 0)
            {
                bool atCheckpoint = CanOpenCampaign();
                var options = node.choices;
                for (int i=0;i<options.Length;i++)
                {
                    GUI.enabled = atCheckpoint && CostaCampaign.Available(options[i],progress);
                    string title = options[i].optional ? L("Optional  ·  ", "Opcional  ·  ") + options[i].Caption : options[i].Caption;
                    if (CostaUi.Button(new Rect(18,160+i*40,577,35),title,serviceButton))
                    {
                        if (session.Online)
                            session.CampaignRequest(node.id,i);
                        else
                        {
                            CostaCheckpoint checkpoint = CaptureCheckpoint(node.id);
                            if (checkpoint == null)
                                ShowWorldNotice(L("Stop in a safe place to save the campaign.", "Pare em um lugar seguro para salvar a campanha."));
                            else if (campaign.TryAdvance(jobs.Ledger, jobs.Account, i, node.id, 0, player.transform.position,
                                car != null, CurrentBag, car?.Speed ?? 0, checkpoint))
                            {
                                CloseOverlay();
                                if (!options[i].optional && car == null)
                                    TryCinematic(string.IsNullOrEmpty(options[i].cinematic) ? node.cinematic : options[i].cinematic);
                            }
                            else
                                ShowWorldNotice(L("Campaign could not be saved.", "Não foi possível salvar a campanha."));
                        }
                    }
                    GUI.enabled = true;
                }
            }
            GUI.Label(new Rect(18,297,310,25),L("Decision history","Histórico de decisões"));
            if (!session.Online && !jobs.InProgress && jobs.Account.checkpoint?.node == node.id &&
                CostaUi.Button(new Rect(400,293,195,28),L("Restart stage","Reiniciar etapa"),serviceButton))
            {
                CloseOverlay();
                RestoreCheckpoint();
            }
            var decisions = progress?.decisions;
            if (decisions != null)
                for (int i=0;i<6 && i<decisions.Length;i++)
                    GUI.Label(new Rect(18,327+i*20,577,21),"· "+campaign.Caption(decisions[decisions.Length-1-i]));
            EndServiceFrame();
        }
    }
}
