using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Six sliders and the <c>AutoPickup</c> debug toggle (issue #63) wired to <see cref="HeroBehaviour"/> (issue #27). Each slider writes its
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
        [SerializeField, Tooltip("0 fights what is nearest the hero, 1 what is nearest the origin (spatial-combat spec).")]
        private ValueSlider originWeightSlider;
        [SerializeField] private RarityFilterSlider lootFilterSlider;
        [SerializeField] private SimSpeedSlider simSpeedSlider;
        [SerializeField, Tooltip("Debug (issue #63): on picks up what the filter admits, off leaves every item on the ground.")]
        private AutoPickupToggle autoPickupToggle;

        private HeroBehaviour _behaviour;

        protected override void BeforeAppear()
        {
            base.BeforeAppear();

            BindBehaviour();

            // BeforeAppear runs on every fade-in and the panel stays enabled between them, so
            // OnPanelDisable is not a reliable pair — detach before attach so a re-appear cannot
            // stack a second listener that writes the behaviour once per duplicate on each drag.
            SetSliderListeners(add: false);
            SetSliderListeners(add: true);

            // A hero load (#114) replaces the Hero and with it the Profile: the sliders go on
            // writing the new one and show its values.
            _ = Session.TrySubscribeHeroLoaded(BindBehaviour);
        }

        // The Hero's Behaviour Profile: the sliders write it live, and the sim reads it live.
        private void BindBehaviour()
        {
            _behaviour = Session.Instance.Hero.Behaviour;

            ApplyAll();
        }

        private void SetSliderListeners(bool add)
        {
            Wire(retreatHealthSlider, OnRetreatHealthChanged, add);
            Wire(recallBagFillSlider, OnRecallBagFillChanged, add);
            Wire(resourceReserveSlider, OnResourceReserveChanged, add);
            Wire(originWeightSlider, OnOriginWeightChanged, add);
            Wire(lootFilterSlider, OnLootFilterChanged, add);
            Wire(simSpeedSlider, OnSimSpeedChanged, add);

            if (autoPickupToggle != null)
            {
                autoPickupToggle.Toggled -= OnAutoPickupToggled;
                if (add) autoPickupToggle.Toggled += OnAutoPickupToggled;
            }

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
            Session.UnsubscribeHeroLoaded(BindBehaviour);

            _behaviour = null;
        }

        private void ApplyAll()
        {
            if (_behaviour == null) return;

            if (retreatHealthSlider != null) retreatHealthSlider.SetValueWithoutNotify(_behaviour.RetreatHealthFraction);
            if (recallBagFillSlider != null) recallBagFillSlider.SetValueWithoutNotify(_behaviour.RecallBagFillFraction);
            if (resourceReserveSlider != null) resourceReserveSlider.SetValueWithoutNotify(_behaviour.CastThreshold);
            if (originWeightSlider != null) originWeightSlider.SetValueWithoutNotify(_behaviour.OriginWeight);
            if (lootFilterSlider != null) lootFilterSlider.SetStepIndexWithoutNotify(HeroBehaviour.RarityIndex(_behaviour.LootFilterMinimum));
            if (simSpeedSlider != null) simSpeedSlider.SetSimSpeedWithoutNotify(_behaviour.SimSpeed);
            if (autoPickupToggle != null) autoPickupToggle.SyncToggle(_behaviour.AutoPickup);
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

        private void OnOriginWeightChanged(float value)
        {
            if (_behaviour != null) _behaviour.OriginWeight = value;
        }

        private void OnLootFilterChanged(float value)
        {
            if (_behaviour != null) _behaviour.LootFilterMinimum = lootFilterSlider.Selected;
        }

        private void OnAutoPickupToggled(bool on)
        {
            if (_behaviour != null) _behaviour.AutoPickup = on;
        }

        private void OnSimSpeedChanged(float value)
        {
            if (_behaviour != null) _behaviour.SimSpeed = simSpeedSlider.SimSpeed;
        }
    }
}
