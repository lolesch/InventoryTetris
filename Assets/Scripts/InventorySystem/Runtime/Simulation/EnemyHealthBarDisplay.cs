using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The health bar of one enemy figure (issue #60, now a child of <see cref="EnemyView"/>), bound to one
    /// <see cref="Enemy"/> at a time (issue #94). It adds only what the enemy has and the hero's
    /// resource globes do not — the archetype name — and hands the rest to the same
    /// <see cref="ResourceDisplay"/> the hero's HUD uses, bound to <see cref="Enemy.HealthResource"/>.
    /// <see cref="EnemyView"/> is the only intended caller of
    /// <see cref="Bind"/>/<see cref="Unbind"/>/<see cref="SetRarityColor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBarDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField, Tooltip("The Health display. The nested HealthBar carries a second, Shield one that enemies never bind.")]
        private ResourceDisplay healthDisplay;
        [SerializeField] private Image rarityBorder;

        /// <summary>Show <paramref name="enemy"/> under its archetype name and follow its health.</summary>
        public void Bind(Enemy enemy)
        {
            if (nameLabel != null)
                nameLabel.text = enemy.Archetype.ToString();

            healthDisplay.Bind(enemy.HealthResource);
        }

        /// <summary>Stop following the bound enemy. Safe when nothing is bound.</summary>
        public void Unbind()
        {
            if (healthDisplay != null)
                healthDisplay.Unbind();
        }

        public void SetRarityColor(ItemRarity rarity)
        {
            if (rarityBorder != null)
                rarityBorder.color = ItemView.RarityColorOf(rarity);
        }
    }
}
