using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class HealthbarDisplay : MonoBehaviour
    {
        [SerializeField] protected Image resourceImage;

        [SerializeField] protected BaseCharacter character;
        [SerializeField] protected StatName resourceName;

        protected void OnEnable()
        {
            if (character)
            {
                var resource = BaseCharacter.GetResource(character, resourceName);

                resource.CurrentHasChanged -= UpdateCurrent;
                resource.CurrentHasChanged += UpdateCurrent;

                UpdateDisplay(resource.CurrentValue);
            }
        }

        protected void OnDisable()
        {
            if (character)
            {
                var resource = BaseCharacter.GetResource(character, resourceName);

                resource.CurrentHasChanged -= UpdateCurrent;
            }
        }

        protected void UpdateCurrent(float current) => UpdateDisplay(current);

        protected virtual void UpdateDisplay(float current)
        {
            if (resourceImage)
                resourceImage.fillAmount = current * 0.01f;
        }
    }
}
