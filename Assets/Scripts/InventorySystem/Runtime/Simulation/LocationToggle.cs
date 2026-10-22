using Submodules.Utility.Extensions;
using Submodules.Utility.Services;
using Submodules.Utility.Tools.Tweening;
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

        [SerializeField, Range(1f, 1.5f), Tooltip("How far the icon swells when this Location opens, to catch the eye.")]
        private float unlockPunchScale = 1.25f;

        [SerializeField, Tooltip("Seconds for each leg of the swell: up, then back.")]
        private float unlockPunchDuration = 0.25f;

        private CanvasGroup group;

        // Null until the first Show of a hero: a Location that is already open when the hero loads did not just unlock.
        private UnlockState? shown;

        /// <summary>
        /// Subscribes in <see cref="OnEnable"/>, not <c>Awake</c>, which a scene object keeps across Play entries
        /// without its subscriptions. A Hidden toggle stays enabled (only its group fades), so it still hears the
        /// signal that reveals it.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            if (!Application.isPlaying || !Session.TrySubscribeHeroLoaded(OnHeroLoaded))
                return;

            SimulationService.Instance.ProgressionChanged -= Refresh;
            SimulationService.Instance.ProgressionChanged += Refresh;
            OnHeroLoaded();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            Session.UnsubscribeHeroLoaded(OnHeroLoaded);

            if (ServiceLocator.IsArmed)
                SimulationService.Instance.ProgressionChanged -= Refresh;
        }

        private void OnHeroLoaded()
        {
            shown = null;
            Refresh();
        }

        private void Refresh() => Show(SimulationService.Instance.StateOf(Location));

        // A cleared Location lets go of the selection, so the same Location can be sent to again. Polled, since a farmed
        // clear raises no signal of its own.
        private void Update()
        {
            if (!Application.isPlaying || !ServiceLocator.IsArmed || !IsOn)
                return;

            var simulation = SimulationService.Instance;
            if (IsClearedHere(simulation.Run, simulation.Locations.ProfileFor(Location)))
                SyncToggle(false);
        }

        private static bool IsClearedHere(RunState run, EncounterProfile profile) =>
            run.Phase == RunPhase.Live && run.Location == profile && run.Encounter is { Phase: SimulationPhase.Complete };

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

            var opened = shown < UnlockState.Active && state >= UnlockState.Active;
            shown = state;

            UnlockLook.Apply(this, state >= UnlockState.Active);

            if (opened)
                PunchOpen();
        }

        // A pointer already over the icon has grown it, and hover wins over the punch (see Interact).
        private void PunchOpen()
        {
            if (!targetGraphic || currentSelectionState == SelectionState.Highlighted)
                return;

            var icon = targetGraphic.transform;
            var rest = IsOn ? hoverScale : 1f;

            Tween.Kill(icon);
            icon.TweenScale(unlockPunchScale, unlockPunchDuration, Ease.OutQuad)
                .OnComplete(() => icon.TweenScale(rest, unlockPunchDuration, Ease.InOutSine));
        }

        // Hover, press and release start their own scale tween, which would fight a running punch for the icon's
        // scale; they cancel it and ease from wherever it got to.
        protected override void Interact(SelectionState state, bool instant)
        {
            if (Application.isPlaying && targetGraphic)
                Tween.Kill(targetGraphic.transform);

            base.Interact(state, instant);
        }

        // A Relocate is refused while a Recall's portal opens (ADR-0022). Refused at the click, before the group moves
        // the highlight, so the selection never diverges from the Run; OnToggle holds the same line for a script's toggle.
        protected override void OnClick()
        {
            if (SimulationService.Instance.Run.IsRecalling)
                return;

            base.OnClick();
        }

        protected override void OnToggle()
        {
            if (!IsOn) return;

            var simulation = SimulationService.Instance;

            // After a Death the Run's down flag is reset but the hero stays dead until Respawn, while this face still shows.
            if (!simulation.CanSend(Location) || simulation.Run.HeroIsDown || simulation.Run.IsRecalling) return;

            var run = simulation.Run;

            // A resume selects the Location its Run is already live at: the toggle follows the Run, it does not command it.
            // A cleared one is sent to again.
            var profile = simulation.Locations.ProfileFor(Location);
            if (run.Phase == RunPhase.Live && run.Location == profile && !IsClearedHere(run, profile))
                return;

            // Switching mid-Run moves the Run, live or held — no Return, so the Run never passes through
            // Town (which would collapse the InFields face and clear this very toggle with it).
            if (run.Phase == RunPhase.None)
                simulation.Send(Location);
            else
                simulation.Relocate(Location);
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
