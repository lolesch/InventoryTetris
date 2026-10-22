using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.GUI.Displays;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The health bar of one <see cref="Figure"/> (issues #60, #94), bound to one health resource at a time. It adds
    /// only a name label to the same <see cref="ResourceDisplay"/> the hero's HUD uses. <see cref="Figure"/> is the
    /// only intended caller of <see cref="Bind"/>/<see cref="Unbind"/>/<see cref="SetRarityColor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HealthBarDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField, Tooltip("The Health display. The nested HealthBar carries a second, Shield one that only the hero binds.")]
        private ResourceDisplay healthDisplay;
        [SerializeField, Tooltip("The Shield display on the same HealthBar.")]
        private ResourceDisplay shieldDisplay;
        [SerializeField, Tooltip("The Shield overlay's root: off for a fighter without a shield, so an enemy shows no empty overlay.")]
        private GameObject shieldRoot;
        [SerializeField] private Image rarityBorder;

        /// <summary>Show <paramref name="health"/> under <paramref name="label"/>, and <paramref name="shield"/> over it when given.</summary>
        public void Bind(CharacterResource health, string label, CharacterResource shield = null)
        {
            if (nameLabel != null)
                nameLabel.text = label;

            healthDisplay.Bind(health);

            if (shieldRoot != null)
                shieldRoot.SetActive(shield != null);

            if (shield != null)
                shieldDisplay.Bind(shield);
            else if (shieldDisplay != null)
                shieldDisplay.Unbind();
        }

        /// <summary>Stop following the bound resources. Safe when nothing is bound.</summary>
        public void Unbind()
        {
            if (healthDisplay != null)
                healthDisplay.Unbind();

            if (shieldDisplay != null)
                shieldDisplay.Unbind();
        }

        public void SetRarityColor(ItemRarity rarity)
        {
            if (rarityBorder != null)
                rarityBorder.color = UiColors.Rarity(rarity);
        }
    }
}
