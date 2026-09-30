using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// One pooled row in the combat panel's enemy HP bar list (issue #60), bound to one
    /// <see cref="Enemy"/> at a time (issue #94): it renders that enemy's
    /// <see cref="Enemy.HealthResource"/> and follows its <c>CurrentHasChanged</c> until
    /// <see cref="Unbind"/>. <see cref="EnemyHealthBarPool"/> is the only intended caller of
    /// <see cref="Bind"/>/<see cref="Unbind"/>/<see cref="SetRarityColor"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBarDisplay : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private Image fillBar;
        [SerializeField] private TextMeshProUGUI numericOverlay;
        [SerializeField] private Image rarityBorder;

        private Enemy _enemy;
        private string _label;

        /// <summary>
        /// Show <paramref name="enemy"/> under its archetype name and follow its health. The
        /// first refresh is read straight off the resource — the enemy's constructor already
        /// raised its own change event before anyone could listen.
        /// </summary>
        public void Bind(Enemy enemy)
        {
            Unbind();

            _enemy = enemy;
            _label = enemy.Archetype.ToString();
            _enemy.HealthResource.CurrentHasChanged += OnHealthChanged;
            Refresh(_label, _enemy.HealthFraction, _enemy.Health, _enemy.MaxHealth);
        }

        /// <summary>Stop following the bound enemy. Safe when nothing is bound.</summary>
        public void Unbind()
        {
            if (_enemy == null)
                return;

            _enemy.HealthResource.CurrentHasChanged -= OnHealthChanged;
            _enemy = null;
        }

        // CurrentHasChanged raises before CurrentValue is written, so the numbers come from the
        // arguments — reading _enemy.Health here would show the value from before the hit.
        private void OnHealthChanged(float previous, float current, float total) =>
            Refresh(_label, total <= 0f ? 0f : current / total, current, total);

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
