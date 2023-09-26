using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class ResourceDisplay : MonoBehaviour
    {
        [SerializeField] protected Image resourceImage;
        // [SerializeField] protected Image impactImage; // TODO: look it up in RuadhWarbands

        [SerializeField] protected TextMeshProUGUI percentageText;
        [SerializeField] protected TextMeshProUGUI currentText;
        [SerializeField] protected TextMeshProUGUI recoveryText;

        [SerializeField] protected BaseCharacter character;
        [SerializeField] protected StatName resourceName = StatName.Health;
        [SerializeField] protected StatName recoveryName = StatName.HealthRegeneration;

        [SerializeField] protected AnimationCurve globeVolume;

        protected void OnEnable()
        {
            if (character)
            {
                var resource = character.GetResource(resourceName);

                resource.CurrentHasChanged -= UpdateDisplay;
                resource.CurrentHasChanged += UpdateDisplay;

                var stat = character.GetStat(recoveryName);

                stat.TotalHasChanged -= UpdateRechargeDisplay;
                stat.TotalHasChanged += UpdateRechargeDisplay;

                UpdateDisplay(0, resource.CurrentValue, resource.TotalValue);
                UpdateRechargeDisplay(stat.TotalValue);
            }
        }

        protected void OnDisable()
        {
            if (character)
            {
                var resource = character.GetResource(resourceName);

                resource.CurrentHasChanged -= UpdateDisplay;

                var stat = character.GetStat(recoveryName);

                stat.TotalHasChanged -= UpdateRechargeDisplay;
            }
        }

        protected virtual void UpdateDisplay(float previous, float current, float total)
        {
            if (resourceImage)
                if (0 < globeVolume.length)
                    resourceImage.fillAmount = globeVolume.Evaluate(current / total);// test filling up fast, then slow then fast, as the volume of the globe would in real life
                else
                    resourceImage.fillAmount = current / total;

            if (percentageText)
                percentageText.text = $"{current / total * 100:0} %";

            if (currentText)
                currentText.text = $"{current:0} / {total:0}";
        }

        protected virtual void UpdateRechargeDisplay(float total)
        {
            if (recoveryText)
                recoveryText.text = $"{total:0} / sec";
        }
    }
}
