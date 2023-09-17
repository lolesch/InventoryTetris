using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class ResourceDisplay : MonoBehaviour
    {
        [SerializeField] protected Image resourceImage;

        [SerializeField] protected BaseCharacter character;
        [SerializeField] protected StatName resourceName = StatName.Health;

        protected void OnEnable()
        {
            if (character)
            {
                var resource = BaseCharacter.GetResource(character, resourceName);

                resource.CurrentHasChanged -= UpdateDisplay;
                resource.CurrentHasChanged += UpdateDisplay;

                UpdateDisplay(0, resource.CurrentValue, resource.TotalValue);
            }
        }

        protected void OnDisable()
        {
            if (character)
            {
                var resource = BaseCharacter.GetResource(character, resourceName);

                resource.CurrentHasChanged -= UpdateDisplay;
            }
        }

        protected virtual void UpdateDisplay(float previous, float current, float total)
        {
            if (resourceImage)
                resourceImage.fillAmount = current / total;// todo: test filling up fast, then slow then fast, as the volume of the globe would in real life
        }
    }
}
