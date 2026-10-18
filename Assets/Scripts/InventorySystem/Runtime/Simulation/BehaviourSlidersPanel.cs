using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Four sliders and the <c>AutoPickup</c> debug toggle (issue #63) wired to <see cref="HeroBehaviour"/> (issue #27). Each slider writes its
    /// value on its <see cref="AbstractSlider.OnValueChanged"/> event — read live, never polled — and
    /// shows its own readout. The sim-speed slider uses a logarithmic response curve: a slider
    /// position of 0 maps to 1x, and 1 maps to ~8x, giving fine control at low speeds where the
    /// player spends most of their time. No pause position exists — the hero always fights at 1x
    /// or faster.
    ///
    /// <see cref="lootFilterSlider"/> offers the <see cref="HeroBehaviour.RaritySteps"/> members
    /// (Common, Magic, Rare, Unique) as four snapping positions.
    ///
    /// Lives on the Combat Panel, a child of the Field face. Its own visibility is derived from the
    /// Inventory Context by its <see cref="ToolSmiths.InventorySystem.GUI.Components.Panels.SidePanel"/>, never from the
    /// Run: it is up on <c>None</c> and <c>Ground</c>, including during the Go Venture preview. Subscribes
    /// to slider events on enable and again in <see cref="SimplePanel.BeforeAppear"/>, so they are live
    /// as soon as the panel becomes visible - including a panel the scene authors as already up.
    ///
    /// The live-encounter and last-run readouts that used to live here moved to
    /// <see cref="EncounterStatsText"/> (issue #61), as its own sibling section between this
    /// panel and the enemy HP bar pool.
    /// </summary>
    public sealed class BehaviourSlidersPanel : SimplePanel
    {
        [Header("Hero behaviour sliders")]
        [SerializeField] private ValueSlider resourceReserveSlider;
        [SerializeField, Tooltip("0 fights what is nearest the hero, 1 what is nearest the origin (spatial-combat spec).")]
        private ValueSlider originWeightSlider;
        [SerializeField] private RarityFilterSlider lootFilterSlider;
        [SerializeField] private SimSpeedSlider simSpeedSlider;
        [SerializeField, Tooltip("Debug (issue #63): on picks up what the filter admits, off leaves every item on the ground.")]
        private EventToggle autoPickupToggle;

        private HeroBehaviour _behaviour;

        // A panel authored as its PanelGroup's active member is up from Awake without ever appearing
        // (the group refuses to deactivate it, so nothing calls BeforeAppear): enabling must wire the
        // sliders too, or they show the saved values and write nothing.
        private void OnEnable() => Attach();

        protected override void BeforeAppear()
        {
            base.BeforeAppear();

            Attach();
        }

        private void Attach()
        {
            // A hero load (#114) replaces the Hero and with it the Profile: the sliders go on
            // writing the new one and show its values. Nothing to wire with no Session armed
            // (an enable in Edit Mode).
            if (!Session.TrySubscribeHeroLoaded(BindBehaviour))
                return;

            BindBehaviour();

            // Enable and every fade-in both land here and the panel stays enabled between fade-ins,
            // so detach before attach: a second listener would write the behaviour once per
            // duplicate on each drag.
            SetSliderListeners(add: false);
            SetSliderListeners(add: true);
        }

        // The Hero's Behaviour Profile: the sliders write it live, and the sim reads it live.
        private void BindBehaviour()
        {
            _behaviour = Session.Instance.Hero.Behaviour;

            ApplyAll();
        }

        private void SetSliderListeners(bool add)
        {
            Wire(resourceReserveSlider, ChangeCastThreshold, add);
            Wire(originWeightSlider, ChangeOriginWeight, add);
            Wire(lootFilterSlider, ChangeLootFilter, add);
            Wire(simSpeedSlider, ChangeSimSpeed, add);

            if (autoPickupToggle != null)
            {
                autoPickupToggle.Toggled -= ToggleAutoPickup;
                if (add) autoPickupToggle.Toggled += ToggleAutoPickup;
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

            if (resourceReserveSlider != null) resourceReserveSlider.SetValueWithoutNotify(_behaviour.CastThreshold);
            if (originWeightSlider != null) originWeightSlider.SetValueWithoutNotify(_behaviour.OriginWeight);
            if (lootFilterSlider != null) lootFilterSlider.SetStepIndexWithoutNotify(HeroBehaviour.RarityIndex(_behaviour.LootFilterMinimum));
            if (simSpeedSlider != null) simSpeedSlider.SetSimSpeedWithoutNotify(_behaviour.SimSpeed);
            if (autoPickupToggle != null) autoPickupToggle.SyncToggle(_behaviour.AutoPickup);
        }

        private void ChangeCastThreshold(float value)
        {
            if (_behaviour != null) _behaviour.CastThreshold = value;
        }

        private void ChangeOriginWeight(float value)
        {
            if (_behaviour != null) _behaviour.OriginWeight = value;
        }

        private void ChangeLootFilter(float value)
        {
            if (_behaviour != null) _behaviour.LootFilterMinimum = lootFilterSlider.Selected;
        }

        private void ToggleAutoPickup(bool on)
        {
            if (_behaviour != null) _behaviour.AutoPickup = on;
        }

        private void ChangeSimSpeed(float value)
        {
            if (_behaviour != null) _behaviour.SimSpeed = simSpeedSlider.SimSpeed;
        }
    }
}
