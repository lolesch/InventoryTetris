using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.GUI.Components.Toggles;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.Serialization;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// A Location of the InFields face. It carries no phase subscription: its selection is cleared by
    /// its <see cref="AbstractToggle.RadioGroup"/>, which resets itself when the face it sits under
    /// has finished fading out (<see cref="ToggleGroup.ResetWithParentPanel"/>) - a
    /// selection never outlives the panel that showed it. That ties the clear to the face rather
    /// than to the Run phase: on Death or Return the highlight lasts one fade longer than the phase,
    /// and <see cref="RunPhasePanel"/> is what guarantees the face does go.
    /// </summary>
    public sealed class LocationToggle : AbstractToggle
    {
        [field: Space]
        [field: FormerlySerializedAs("location"), SerializeField] public LocationConfig Location { get; private set; }

        [SerializeField, Tooltip("Where the hero stands at this Location, and where its arena is drawn (ADR-0021). Empty: this toggle's own centre.")]
        private RectTransform travelPoint;

        /// <summary>The rect whose centre is this Location on the map: the hero walks to it and the arena is drawn there.</summary>
        public RectTransform TravelPoint => this.TravelPointOr(travelPoint);

        private CanvasGroup group;

        /// <summary>
        /// Subscribes in <see cref="OnEnable"/>, not <c>Awake</c>, which a scene object keeps across Play entries
        /// without its subscriptions. A Hidden toggle stays enabled (only its group fades), so it still hears the
        /// signal that reveals it.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            if (!Application.isPlaying || !Session.TrySubscribeHeroLoaded(Refresh))
                return;

            SimulationService.Instance.ProgressionChanged -= Refresh;
            SimulationService.Instance.ProgressionChanged += Refresh;
            Refresh();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Session.UnsubscribeHeroLoaded(Refresh);

            if (ServiceLocator.IsArmed)
                SimulationService.Instance.ProgressionChanged -= Refresh;
        }

        private void Refresh() => Show(SimulationService.Instance.StateOf(Location));

        /// <summary>
        /// Hidden is not seen or hit; Locked is the non-interactable look (<see cref="UnlockLook"/>), so it neither
        /// scales on hover nor takes a click; Active and Complete are open.
        /// </summary>
        private void Show(UnlockState state)
        {
            if (!group && !TryGetComponent(out group))
                group = gameObject.AddComponent<CanvasGroup>();

            group.alpha = state == UnlockState.Hidden ? 0f : 1f;
            group.blocksRaycasts = state != UnlockState.Hidden;

            UnlockLook.Apply(this, state >= UnlockState.Active);
        }

        protected override void OnToggle()
        {
            if (!IsOn) return;

            var simulation = SimulationService.Instance;

            // After a Death the Run's down flag is reset but the hero stays dead until Respawn, while this face still shows.
            if (!simulation.CanSend(Location) || simulation.Run.HeroIsDown) return;

            // Switching mid-Run moves the live Run — no Return, so the Run never passes through
            // Town (which would collapse the InFields face and clear this very toggle with it).
            if (simulation.Run.Phase == RunPhase.Live)
                simulation.Relocate(Location);
            else
                simulation.Send(Location);
        }

#if UNITY_EDITOR
        /// <summary>The group's reset is the only thing that clears a Location, so a group of them
        /// without <see cref="ToggleGroup.ResetWithParentPanel"/> keeps a stale selection
        /// after the Run ends - and nothing else would say so.</summary>
        protected override void OnValidate()
        {
            base.OnValidate();

            if (RadioGroup && !RadioGroup.ResetWithParentPanel)
                Debug.LogWarning($"{name}: its ToggleGroup '{RadioGroup.name}' does not reset with its parent " +
                                 "panel - a Location stays selected after the Run ends. Enable " +
                                 "'ResetWithParentPanel' on the group.", RadioGroup);
        }
#endif // UNITY_EDITOR
    }
}
