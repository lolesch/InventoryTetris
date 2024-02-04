using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class SkillSlot : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownOverlay;

        private void OnEnable() => SetCooldownFillAmount(0);

        public void SetIcon(Sprite sprite)
        {
            if (!icon)
                return;

            if (icon.sprite != sprite)
                icon.sprite = sprite;
        }

        public void SetCooldownFillAmount(float fillAmount)
        {
            if (!cooldownOverlay)
                return;

            if (cooldownOverlay.fillAmount != fillAmount)
                cooldownOverlay.fillAmount = fillAmount;
        }
    }
}
