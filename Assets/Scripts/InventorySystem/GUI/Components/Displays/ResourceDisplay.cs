using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Services;
using Submodules.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public sealed class ResourceDisplay : MonoBehaviour
    {
        [SerializeField] private Image resourceImage;
        // [SerializeField] protected Image impactImage; // TODO: look it up in RuadhWarbands

        [SerializeField] private TextMeshProUGUI percentageText;
        [SerializeField] private TextMeshProUGUI currentText;
        [SerializeField] private TextMeshProUGUI recoveryText;

        [SerializeField, Tooltip("Show the Session's Hero. Off: the display is driven by Bind, like an enemy's health bar.")] private bool followsHero;
        [SerializeField] private StatName resourceName = StatName.Health;
        [SerializeField] private StatName recoveryName = StatName.HealthRegeneration;

        [SerializeField] private AnimationCurve globeVolume;

        private CharacterResource _resource;
        private CharacterStat _recovery;
        private bool _bound;

        /// <summary>
        /// Drive this display from a resource handed in, instead of from the Hero — an
        /// enemy's health, which no Hero owns. <paramref name="recovery"/>
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

        private void OnEnable()
        {
            // No Session armed (an enable in Edit Mode): nothing to follow, and the Hero is not there to read.
            if (!_bound && followsHero && Session.TrySubscribeHeroLoaded(FollowHero))
            {
                var hero = Session.Instance.Hero;
                _resource = hero.GetResource(resourceName);
                _recovery = hero.GetStat(recoveryName);
            }

            Acquire();
        }

        private void OnDisable()
        {
            Session.UnsubscribeHeroLoaded(FollowHero);

            Release();

            if (!_bound)
            {
                _resource = null;
                _recovery = null;
            }
        }

        // A hero load (#114) replaces the Hero and with it its resource and stat: let go of the old
        // ones and follow the new. A display driven by Bind stays on what it was handed.
        private void FollowHero()
        {
            if (_bound || !followsHero)
                return;

            Release();

            var hero = Session.Instance.Hero;
            _resource = hero.GetResource(resourceName);
            _recovery = hero.GetStat(recoveryName);

            Acquire();
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

        private void UpdateDisplay(float previous, float current, float total)
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

        private void UpdateRechargeDisplay(float total)
        {
            if (recoveryText)
                recoveryText.text = $"{total:0} / sec";
        }
    }
}
