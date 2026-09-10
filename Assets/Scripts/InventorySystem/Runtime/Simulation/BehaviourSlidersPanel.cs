using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Five sliders wired to <see cref="HeroBehaviour"/> (issue #27). Each slider writes its
    /// value on the <see cref="Slider.onValueChanged"/> event — read live, never polled. The
    /// sim-speed slider uses a logarithmic response curve: a slider position of 0 maps to 1x,
    /// and 1 maps to ~8x, giving fine control at low speeds where the player spends most of
    /// their time. No pause position exists — the hero always fights at 1x or faster.
    ///
    /// <see cref="lootFilterSlider"/> is a discrete 0..3 integer slider mapped to
    /// <see cref="ItemRarity"/> values via <see cref="HeroBehaviour.RaritySteps"/>:
    /// 0=Common, 1=Magic, 2=Rare, 3=Unique. Whole number positions give a tactile snap;
    /// fractional values are rounded.
    ///
    /// Lives on the Combat Panel, which <see cref="MinimapPanel"/> fades in on Send and out on
    /// Recall / Death. Subscribes to slider events in <see cref="AbstractPanel.BeforeAppear"/>
    /// so they are live as soon as the panel becomes visible.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BehaviourSlidersPanel : AbstractPanel
    {
        [Header("Hero behaviour sliders")]
        [SerializeField] private Slider retreatHealthSlider;
        [SerializeField] private Slider recallBagFillSlider;
        [SerializeField] private Slider resourceReserveSlider;
        [SerializeField] private Slider lootFilterSlider;
        [SerializeField] private Slider simSpeedSlider;

        private HeroBehaviour _behaviour;

        // Cached TMP value displays — resolved from each slider's sibling "Value" child.
        private TextMeshProUGUI _retreatHealthValue;
        private TextMeshProUGUI _recallBagFillValue;
        private TextMeshProUGUI _resourceReserveValue;
        private TextMeshProUGUI _lootFilterValue;
        private TextMeshProUGUI _simSpeedValue;

        protected override void BeforeAppear()
        {
            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            _behaviour = provider.Behaviour;

            // Resolve TMP value displays from hierarchy: each slider's parent row
            // contains a child named "Value" with a TextMeshProUGUI.
            _retreatHealthValue = FindValueDisplay(retreatHealthSlider);
            _recallBagFillValue = FindValueDisplay(recallBagFillSlider);
            _resourceReserveValue = FindValueDisplay(resourceReserveSlider);
            _lootFilterValue = FindValueDisplay(lootFilterSlider);
            _simSpeedValue = FindValueDisplay(simSpeedSlider);

            ApplyAll();

            // BeforeAppear runs on every fade-in and the panel stays enabled between them, so
            // OnDisable is not a reliable pair — detach before attach so a re-appear cannot
            // stack a second listener that writes the behaviour once per duplicate on each drag.
            SetSliderListeners(add: false);
            SetSliderListeners(add: true);
        }

        private void SetSliderListeners(bool add)
        {
            Wire(retreatHealthSlider, OnRetreatHealthChanged, add);
            Wire(recallBagFillSlider, OnRecallBagFillChanged, add);
            Wire(resourceReserveSlider, OnResourceReserveChanged, add);
            Wire(lootFilterSlider, OnLootFilterChanged, add);
            Wire(simSpeedSlider, OnSimSpeedChanged, add);

            static void Wire(Slider slider, UnityEngine.Events.UnityAction<float> handler, bool add)
            {
                if (slider == null) return;
                if (add) slider.onValueChanged.AddListener(handler);
                else slider.onValueChanged.RemoveListener(handler);
            }
        }

        private void OnDisable()
        {
            SetSliderListeners(add: false);

            _behaviour = null;
        }

        /// <summary>
        /// Find a "Value" TextMeshProUGUI sibling within the slider's parent row.
        /// Returns null when the hierarchy doesn't contain one — the display methods
        /// already null-check, so this is safe.
        /// </summary>
        private static TextMeshProUGUI FindValueDisplay(Slider slider)
        {
            if (slider == null) return null;
            var parent = slider.transform.parent;
            if (parent == null) return null;
            var valueTransform = parent.Find("Value");
            return valueTransform != null ? valueTransform.GetComponent<TextMeshProUGUI>() : null;
        }

        private void ApplyAll()
        {
            if (_behaviour == null) return;

            SetSliderValue(retreatHealthSlider, _behaviour.RetreatHealthFraction);
            SetSliderValue(recallBagFillSlider, _behaviour.RecallBagFillFraction);
            SetSliderValue(resourceReserveSlider, _behaviour.CastThreshold);
            SetSliderValue(lootFilterSlider, HeroBehaviour.RarityIndex(_behaviour.LootFilterMinimum));
            SetSliderValue(simSpeedSlider, HeroBehaviour.SimSpeedToSlider(_behaviour.SimSpeed));

            RefreshDisplays();
        }

        private void RefreshDisplays()
        {
            UpdatePercentDisplay(retreatHealthSlider, _retreatHealthValue);
            UpdatePercentDisplay(recallBagFillSlider, _recallBagFillValue);
            UpdatePercentDisplay(resourceReserveSlider, _resourceReserveValue);
            UpdateLootDisplay(lootFilterSlider, _lootFilterValue);
            UpdateSpeedDisplay(simSpeedSlider, _simSpeedValue);
        }

        private void OnRetreatHealthChanged(float value)
        {
            if (_behaviour != null) _behaviour.RetreatHealthFraction = Mathf.Clamp01(value);
            UpdatePercentDisplay(retreatHealthSlider, _retreatHealthValue);
        }

        private void OnRecallBagFillChanged(float value)
        {
            if (_behaviour != null) _behaviour.RecallBagFillFraction = Mathf.Clamp01(value);
            UpdatePercentDisplay(recallBagFillSlider, _recallBagFillValue);
        }

        private void OnResourceReserveChanged(float value)
        {
            if (_behaviour != null) _behaviour.CastThreshold = Mathf.Clamp01(value);
            UpdatePercentDisplay(resourceReserveSlider, _resourceReserveValue);
        }

        private void OnLootFilterChanged(float value)
        {
            if (_behaviour == null) return;

            var index = Mathf.Clamp(Mathf.RoundToInt(value), 0, HeroBehaviour.RaritySteps.Length - 1);
            _behaviour.LootFilterMinimum = HeroBehaviour.RarityForIndex(index);
            UpdateLootDisplay(lootFilterSlider, _lootFilterValue);
        }

        private void OnSimSpeedChanged(float value)
        {
            if (_behaviour != null) _behaviour.SimSpeed = HeroBehaviour.SliderToSimSpeed(value);
            UpdateSpeedDisplay(simSpeedSlider, _simSpeedValue);
        }

        private static void SetSliderValue(Slider slider, float value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
        }

        private static void UpdatePercentDisplay(Slider slider, TextMeshProUGUI text)
        {
            if (text == null || slider == null) return;
            text.text = (slider.value * 100f).ToString("0") + "%";
        }

        private static void UpdateLootDisplay(Slider slider, TextMeshProUGUI text)
        {
            if (text == null || slider == null) return;
            var index = Mathf.Clamp(Mathf.RoundToInt(slider.value), 0, HeroBehaviour.RaritySteps.Length - 1);
            text.text = HeroBehaviour.RarityForIndex(index).ToString();
        }

        private static void UpdateSpeedDisplay(Slider slider, TextMeshProUGUI text)
        {
            if (text == null || slider == null) return;
            var speed = HeroBehaviour.SliderToSimSpeed(slider.value);
            text.text = speed.ToString("0.0") + "x";
        }
    }
}
