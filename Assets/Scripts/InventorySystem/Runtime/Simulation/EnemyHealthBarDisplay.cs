using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// One pooled row in the combat panel's enemy HP bar list (issue #60). UI shell only —
    /// binding to per-enemy <see cref="Simulation.Enemy"/> instances from the
    /// <c>EncounterSimulation</c> is a separate ticket; <see cref="EnemyHealthBarPool"/> is the
    /// only intended caller of <see cref="Refresh"/>/<see cref="SetRarityColor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBarDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private Image fillBar;
        [SerializeField] private TextMeshProUGUI numericOverlay;
        [SerializeField] private Image rarityBorder;

        public void Refresh(string label, float hpFraction, float current, float max)
        {
            if (nameLabel != null)
                nameLabel.text = label;

            if (fillBar != null)
                fillBar.fillAmount = Mathf.Clamp01(hpFraction);

            if (numericOverlay != null)
                numericOverlay.text = $"{current:0}/{max:0}";
        }

        public void SetRarityColor(ItemRarity rarity)
        {
            if (rarityBorder != null)
                rarityBorder.color = ItemView.RarityColorOf(rarity);
        }
    }
}
