using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using Submodules.Utility.Extensions;
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

        private CharacterResource _resource;
        private CharacterStat _recovery;
        private bool _bound;

        /// <summary>
        /// Drive this display from a resource handed in, instead of from <c>character</c> — an
        /// enemy's health, which no <see cref="BaseCharacter"/> owns. <paramref name="recovery"/>
        /// is optional: without it the recovery text is left alone. Stays bound across a
        /// disable/enable (a pooled bar) until <see cref="Unbind"/>.
        /// </summary>
        public void Bind(CharacterResource resource, CharacterStat recovery = null)
        {
            Release();

            _resource = resource;
            _recovery = recovery;
            _bound = true;

            if (isActiveAndEnabled)
                Acquire();
        }

        /// <summary>Stop following the bound resource. Safe when nothing is bound.</summary>
        public void Unbind()
        {
            Release();

            _resource = null;
            _recovery = null;
            _bound = false;
        }

        protected void OnEnable()
        {
            if (!_bound && character)
            {
                _resource = character.GetResource(resourceName);
                _recovery = character.GetStat(recoveryName);
            }

            Acquire();
        }

        protected void OnDisable()
        {
            Release();

            if (!_bound)
            {
                _resource = null;
                _recovery = null;
            }
        }

        private void Acquire()
        {
            if (_resource != null)
            {
                _resource.CurrentHasChanged -= UpdateDisplay;
                _resource.CurrentHasChanged += UpdateDisplay;

                UpdateDisplay(0, _resource.CurrentValue, _resource.TotalValue);
            }

            if (_recovery != null)
            {
                _recovery.TotalHasChanged -= UpdateRechargeDisplay;
                _recovery.TotalHasChanged += UpdateRechargeDisplay;

                UpdateRechargeDisplay(_recovery.TotalValue);
            }
        }

        private void Release()
        {
            if (_resource != null)
                _resource.CurrentHasChanged -= UpdateDisplay;

            if (_recovery != null)
                _recovery.TotalHasChanged -= UpdateRechargeDisplay;
        }

        protected virtual void UpdateDisplay(float previous, float current, float total)
        {
            if (resourceImage)
                if (0 < globeVolume.length)
                    resourceImage.fillAmount = globeVolume.Evaluate(current / total);
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
