using System;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Holds the mid-loop state a Run needs — the <see cref="RunState"/> FSM, the
    /// live <see cref="HeroBehaviour"/> value and the selected <see cref="LocationConfig"/>
    /// (spec <i>Persistence constraints</i>: its own provider, <b>not</b> a member on the
    /// <c>InventoryProvider</c> god object). It builds the <see cref="EncounterSimulation"/>
    /// through the factory <see cref="RunState"/> is handed — binding the live hero
    /// adapter, the <see cref="UnityRollSource"/> and <c>HeroBehaviour.Engagement</c> — and owns
    /// the one seam the engine adds: while <see cref="RunPhase.InField"/> the sim drives the
    /// hero's regen, so the hero's <see cref="BaseCharacter.SuppressRegen"/> is raised for the
    /// duration and cleared when the Run ends (issue #43).
    ///
    /// The frame-by-frame tick is <see cref="SimulationDriver"/>'s job; a debug Send / Recall
    /// panel (<see cref="SimulationDebugPanel"/>) drives the smoke gate until the real map UI
    /// lands (issue #27). Both are attached to this provider's GameObject on <see cref="Awake"/>
    /// so a bare scene needs no wiring.
    /// </summary>
    public sealed class SimulationProvider : AbstractProvider<SimulationProvider>
    {
        [Tooltip("MVP flat Cast cost the hero adapter reports - no gear stat for it yet (ADR-0010, /prototype starting point 16).")]
        [SerializeField] private float castCost = HeroCombatant.DefaultCastCost;

        [Header("Behaviour defaults (issue #27's sliders write these live)")]
        [SerializeField, Range(1f, 8f)] private float simSpeed = 1f;
        [SerializeField, Min(1)] private int engagement = 3;
        [SerializeField, Range(0f, 1f)] private float retreatHealthFraction;
        [SerializeField, Range(0f, 1f)] private float recallBagFillFraction = 1f;
        [SerializeField, Range(0f, 1f)] private float castThreshold;
        [SerializeField] private ItemRarity lootFilterMinimum = ItemRarity.Common;

        private readonly UnityRollSource _rolls = new();

        private RunState _run;
        private BaseCharacter _suppressed;

        /// <summary>The Run FSM — <see cref="RunPhase.InTown"/> until a <see cref="Send"/>.</summary>
        public RunState Run => _run ??= BuildRun();

        /// <summary>
        /// The six slider-fed values that steer the hero (issue #23). Seeded here with inert
        /// MVP defaults; issue #27's sliders overwrite them on their change event. Read live —
        /// never snapshotted.
        /// </summary>
        public HeroBehaviour Behaviour { get; } = new();

        /// <summary>The Location a <see cref="Send"/> will (or last did) go to — persisted per the save constraints.</summary>
        public LocationConfig SelectedLocation { get; private set; }

#if UNITY_EDITOR
        // Zero-setup smoke gate (issue #43): spawn the provider — and with it the driver and the
        // debug panel — on entering play mode so a bare scene needs nothing added. Issue #27's
        // real map UI replaces the panel and this hook goes with it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawnForSmokeGate() => _ = Instance;
#endif

        private void Awake()
        {
            ApplyBehaviourDefaults();

            if (!TryGetComponent<SimulationDriver>(out _))
                _ = gameObject.AddComponent<SimulationDriver>();

#if UNITY_EDITOR
            if (!TryGetComponent<SimulationDebugPanel>(out _))
                _ = gameObject.AddComponent<SimulationDebugPanel>();
#endif
        }

        private void OnValidate() => ApplyBehaviourDefaults();

        private void ApplyBehaviourDefaults()
        {
            if (Behaviour == null) return;

            Behaviour.SimSpeed = Mathf.Max(1f, simSpeed);
            Behaviour.Engagement = Mathf.Max(1, engagement);
            Behaviour.RetreatHealthFraction = Mathf.Clamp01(retreatHealthFraction);
            Behaviour.RecallBagFillFraction = Mathf.Clamp01(recallBagFillFraction);
            Behaviour.CastThreshold = Mathf.Clamp01(castThreshold);
            Behaviour.LootFilterMinimum = lootFilterMinimum;
        }

        private RunState BuildRun()
        {
            var run = new RunState(StartEncounter);
            run.RunEnded += OnRunEnded;
            return run;
        }

        private EncounterSimulation StartEncounter(EncounterProfile profile)
        {
            // Unity's lifetime-aware == must run here — the null-coalescing operator bypasses it
            // and would bind the adapter to a destroyed LocalPlayer.
            var player = CharacterProvider.Instance.Player;
            if (player == null)
                throw new InvalidOperationException("No LocalPlayer on the CharacterProvider — cannot start an Encounter.");

            var hero = new HeroCombatant(player, Mathf.Max(0f, castCost));
            return new EncounterSimulation(hero, profile, _rolls, Mathf.Max(1, Behaviour.Engagement));
        }

        /// <summary>
        /// Send the hero to <paramref name="location"/> — <see cref="RunPhase.InTown"/> →
        /// <see cref="RunPhase.InField"/>. Selects the Location, opens the Run and hands the
        /// hero's regen to the sim for the duration.
        /// </summary>
        public void Send(LocationConfig location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));

            SelectedLocation = location;
            Run.Send(location.ToProfile());

            _suppressed = CharacterProvider.Instance.Player;
            if (_suppressed != null)
                _suppressed.SuppressRegen = true;
        }

        /// <summary>End the Run with everything kept (spec story 5). Refused once the hero is down — that ends in <see cref="HandleHeroDeath"/>.</summary>
        public RunResult Recall() => Run.Recall();

        /// <summary>
        /// Close a Run whose hero has been downed — <see cref="RunPhase.InField"/> →
        /// <see cref="RunPhase.InTown"/> with the Death penalty (issue #43 passes no XP progress;
        /// the revive-at-a-fraction and the real penalty numbers are issue #45). A no-op unless a
        /// Run is live and the hero is actually down, so the driver can call it every frame.
        /// </summary>
        public void HandleHeroDeath()
        {
            if (Run.Phase == RunPhase.InField && Run.HeroIsDown)
                _ = Run.HandleDeath(0);
        }

        private void OnRunEnded()
        {
            if (_suppressed != null)
            {
                _suppressed.SuppressRegen = false;
                _suppressed = null;
            }
        }
    }
}
