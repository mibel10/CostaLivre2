using System;
using UnityEngine;

namespace CostaLivre
{
    [CreateAssetMenu(menuName = "Costa Livre/Campanha ramificada")]
    public sealed class CostaCampaignDefinition : ScriptableObject
    {
        [SerializeField] private string start = "intro";
        [SerializeField] private CostaCampaign.Node[] objectives;

        internal CostaCampaign Compile() => new(start, objectives);

        [ContextMenu("Validar campanha")]
        private void ValidateCampaign()
        {
            try
            {
                Compile();
                Debug.Log("Campanha válida. Todos os objetivos e finais estão acessíveis.", this);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError(exception.Message, this);
            }
        }
    }
}
