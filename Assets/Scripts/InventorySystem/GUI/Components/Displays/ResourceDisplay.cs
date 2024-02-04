using TMPro;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public class ResourceDisplay : MonoBehaviour, IDisplay<float>, IDisplay<(float previous, float current, float total)>
    {
        [SerializeField] protected Image resourceImage;
        // [SerializeField] protected Image impactImage; // TODO: look it up in previous projects

        [SerializeField] protected TextMeshProUGUI percentageText;
        [SerializeField] protected TextMeshProUGUI currentText;
        [SerializeField] protected TextMeshProUGUI recoveryText;

        //[SerializeField] protected BaseCharacter character => CharacterProvider.Instance.Player;
        [SerializeField] protected CharacterResource resource;
        [SerializeField] protected CharacterStat recoveryStat;
        [SerializeField] protected StatName resourceName = StatName.Health;
        [SerializeField] protected StatName recoveryName = StatName.HealthRegeneration;

        //[SerializeField] protected AnimationCurve globeVolume;

        [ContextMenu("RefreshDisplay")]
        protected void OnEnable()
        {
            if (CharacterProvider.Instance.Player != null)
                SetupDisplay(CharacterProvider.Instance.Player);
            else
            {
                CharacterProvider.Instance.PlayerChanged -= SetupDisplay;
                CharacterProvider.Instance.PlayerChanged += SetupDisplay;
            }
        }

        protected void OnDisable()
        {
            resource.CurrentHasChanged -= UpdateCurrent;

            recoveryStat.TotalHasChanged -= RefreshDisplay;

            CharacterProvider.Instance.PlayerChanged -= SetupDisplay;
        }

        private void SetupDisplay(LocalPlayer player)
        {
            resource = player.GetResource(resourceName);

            resource.CurrentHasChanged -= UpdateCurrent;
            resource.CurrentHasChanged += UpdateCurrent;

            recoveryStat = player.GetStat(recoveryName);

            recoveryStat.TotalHasChanged -= RefreshDisplay;
            recoveryStat.TotalHasChanged += RefreshDisplay;

            RefreshDisplay((0, resource.CurrentValue, resource.TotalValue));
            RefreshDisplay(recoveryStat.TotalValue);
        }

        protected virtual void UpdateCurrent(float previous, float current, float total) => RefreshDisplay((previous, current, total));

        public void RefreshDisplay(float rechargeValue)
        {
            if (recoveryText)
                recoveryText.text = $"{rechargeValue:0} / sec";
        }

        public void RefreshDisplay((float previous, float current, float total) data)
        {
            if (resourceImage)
            {
                //resourceImage.fillAmount = 0 < globeVolume.length ? globeVolume.Evaluate(data.current / data.total) : data.current / data.total;
                resourceImage.fillAmount = data.total != 0 ? data.current / data.total : 0;
            }

            if (percentageText)
            {
                percentageText.text = $"{data.current / data.total * 100:0} %";
                percentageText.gameObject.SetActive(data.total != 0);
            }

            if (currentText)
            {
                currentText.text = $"{data.current:0} / {data.total:0}";
                currentText.gameObject.SetActive(data.total != 0);
            }

            if (recoveryText)
                recoveryText.gameObject.SetActive(data.total != 0);
        }
    }
}
