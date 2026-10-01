using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Five sliders wired to <see cref="HeroBehaviour"/> (issue #27). Each slider writes its
    /// value on its <see cref="AbstractSlider.OnValueChanged"/> event — read live, never polled — and
    /// shows its own readout. The sim-speed slider uses a logarithmic response curve: a slider
    /// position of 0 maps to 1x, and 1 maps to ~8x, giving fine control at low speeds where the
    /// player spends most of their time. No pause position exists — the hero always fights at 1x
    /// or faster.
    ///
    /// <see cref="lootFilterSlider"/> offers the <see cref="HeroBehaviour.RaritySteps"/> members
    /// (Common, Magic, Rare, Unique) as four snapping positions.
    ///
    /// Lives on the Combat Panel, a child of the InFields face. Its own visibility is not
    /// <see cref="RunPhase"/>-driven — <see cref="RunPhase.InField"/> only exists once a Location
    /// is actually Sent to, but the Combat Panel must already be up during the Go Venture preview
    /// (still <see cref="RunPhase.InTown"/>, no Run yet). <see cref="FieldFacePanel"/> cascades
    /// this panel's <see cref="SimplePanel.Expand"/>/<see cref="SimplePanel.Collapse"/> straight from its own appear/
    /// disappear instead, so both follow the same UI-layer event the preview relies on. Subscribes
    /// to slider events in <see cref="SimplePanel.BeforeAppear"/> so they are live as soon as the
    /// panel becomes visible.
    ///
    /// The live-encounter and last-run readouts that used to live here moved to
    /// <see cref="EncounterStatsPanel"/> (issue #61), as its own sibling section between this
    /// panel and the enemy HP bar pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BehaviourSlidersPanel : SimplePanel
    {
        [Header("Hero behaviour sliders")]
        [SerializeField] private ValueSlider retreatHealthSlider;
        [SerializeField] private ValueSlider recallBagFillSlider;
        [SerializeField] private ValueSlider resourceReserveSlider;
        [SerializeField] private RarityFilterSlider lootFilterSlider;
        [SerializeField] private SimSpeedSlider simSpeedSlider;

        private HeroBehaviour _behaviour;

        protected override void BeforeAppear()
        {
            base.BeforeAppear();

            var provider = SimulationProvider.Instance;
            if (provider == null) return;

            _behaviour = provider.Behaviour;

            ApplyAll();

            // BeforeAppear runs on every fade-in and the panel stays enabled between them, so
            // OnPanelDisable is not a reliable pair — detach before attach so a re-appear cannot
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

            static void Wire(AbstractSlider slider, System.Action<float> handler, bool add)
            {
                if (slider == null) return;
                if (add) slider.OnValueChanged += handler;
                else slider.OnValueChanged -= handler;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            SetSliderListeners(add: false);

            _behaviour = null;
        }

        private void ApplyAll()
        {
            if (_behaviour == null) return;

            if (retreatHealthSlider != null) retreatHealthSlider.SetValueWithoutNotify(_behaviour.RetreatHealthFraction);
            if (recallBagFillSlider != null) recallBagFillSlider.SetValueWithoutNotify(_behaviour.RecallBagFillFraction);
            if (resourceReserveSlider != null) resourceReserveSlider.SetValueWithoutNotify(_behaviour.CastThreshold);
            if (lootFilterSlider != null) lootFilterSlider.SetSelectedWithoutNotify(_behaviour.LootFilterMinimum);
            if (simSpeedSlider != null) simSpeedSlider.SetSimSpeedWithoutNotify(_behaviour.SimSpeed);
        }

        private void OnRetreatHealthChanged(float value)
        {
            if (_behaviour != null) _behaviour.RetreatHealthFraction = value;
        }

        private void OnRecallBagFillChanged(float value)
        {
            if (_behaviour != null) _behaviour.RecallBagFillFraction = value;
        }

        private void OnResourceReserveChanged(float value)
        {
            if (_behaviour != null) _behaviour.CastThreshold = value;
        }

        private void OnLootFilterChanged(float value)
        {
            if (_behaviour != null) _behaviour.LootFilterMinimum = lootFilterSlider.Selected;
        }

        private void OnSimSpeedChanged(float value)
        {
            if (_behaviour != null) _behaviour.SimSpeed = simSpeedSlider.SimSpeed;
        }
    }
}
