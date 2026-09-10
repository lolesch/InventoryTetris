using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Runtime.Provider;
using ToolSmiths.InventorySystem.Simulation;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Holds the mid-loop state a Run needs — the <see cref="RunState"/> FSM, the
    /// live <see cref="HeroBehaviour"/> value and the selected <see cref="LocationConfig"/>
    /// (spec <i>Persistence constraints</i>: its own provider, <b>not</b> a member on the
    /// <c>InventoryProvider</c> god object). It builds the <see cref="EncounterSimulation"/>
    /// through the factory <see cref="RunState"/> is handed — binding the live hero
    /// adapter, the <see cref="UnityRollSource"/> and <c>HeroBehaviour.Engagement</c> — and wires
    /// the loot / XP / Corpse delivery (issue #44) that turns each Run into the loop the spec
    /// stories describe.
    ///
    /// The per-kill loot flow (issue #24) is rebuilt for each Encounter — a fresh
    /// <see cref="LootFlow"/> against the real bag and <see cref="Wallet"/> — and drives
    /// <see cref="RunState.BankCurrency"/> per kill so the Death fee reads the real take. XP
    /// settles to the LocalPlayer on each clear. The ADR-0009 Death penalty and the
    /// corpse-recovery rules live in <see cref="RunSettlement"/> (finding #2); this file binds
    /// its <see cref="ContainerSettlementBag"/> / <see cref="PlayerWalletLedger"/> ports and
    /// hands <see cref="Send"/>'s re-entry the live loot ground.
    /// The one correctness detail this file still owns: <see cref="Corpse"/> matches by
    /// <see cref="EncounterProfile"/> <em>reference</em>, so <see cref="_profiles"/> memoizes
    /// one profile per <see cref="LocationConfig"/> and Send / Settle / Recover all share it.
    ///
    /// The frame-by-frame tick is <see cref="SimulationDriver"/>'s job; the real map UI
    /// (<see cref="MapPanel"/>, issue #27) replaces the old debug panel. The driver is
    /// attached to this provider's GameObject on <see cref="Awake"/> so a bare scene needs
    /// no wiring.
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

        [Header("Death penalty (issue #21 — unfrozen balance numbers)")]
        [SerializeField, Range(0f, 1f)] private float xpLossFraction = 0.25f;
        [SerializeField, Range(0f, 1f)] private float currencyFeeFraction = 0.5f;

        private readonly UnityRollSource _rolls = new();

        private RunState _run;
        private LootFlow _lootFlow;
        private ItemGenerator _itemGenerator;
        private readonly Dictionary<LocationConfig, EncounterProfile> _profiles = new();
        private RunSettlement _settlement;

        /// <summary>
        /// The ADR-0009 Death / recovery rules (finding #2), bound to the live bag, Wallet and
        /// hero. Built lazily because the ports resolve their providers on each call - and held
        /// for the provider's lifetime so the one standing <see cref="Corpse"/> persists across
        /// Runs.
        /// </summary>
        private RunSettlement Settlement => _settlement ??= new RunSettlement(
            new ContainerSettlementBag(), new PlayerWalletLedger());

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

        /// <summary>
        /// The live Run's loot flow (issue #24) — <c>null</c> in Town or when the ItemProvider
        /// is not configured. Exposed for the debug panel so a full bag visibly strands loot.
        /// </summary>
        public LootFlow LootFlow => _lootFlow;

#if UNITY_EDITOR
        // Zero-setup smoke gate (issue #43): spawn the provider — and with it the driver —
        // on entering play mode so a bare scene needs nothing added.
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
            var run = new RunState(StartEncounter, new RunPenalty(xpLossFraction, currencyFeeFraction));
            run.RunEnded += OnRunEnded;
            return run;
        }

        /// <summary>
        /// The <see cref="EncounterSimulation"/> factory <see cref="RunState"/> hands its Send —
        /// builds the live Encounter and, on top of it, this Run's loot flow and XP settlement.
        /// </summary>
        private EncounterSimulation StartEncounter(EncounterProfile profile)
        {
            // Unity's lifetime-aware == must run here — the null-coalescing operator bypasses it
            // and would bind the adapter to a destroyed LocalPlayer.
            var player = CharacterProvider.Instance.Player;
            if (player == null)
                throw new InvalidOperationException("No LocalPlayer on the CharacterProvider — cannot start an Encounter.");

            var hero = new HeroCombatant(player, Mathf.Max(0f, castCost));

            // The behaviour goes in by reference, not as a snapshot of its values: the sliders
            // (issue #27) write it live, and Engagement, the Cast threshold and both retreat
            // triggers are all read off it inside the tick (issue #23).
            var encounter = new EncounterSimulation(hero, profile, _rolls, Behaviour, bag: BagGauge());

            // XP settles on each Encounter clear, independent of whether a loot system is
            // configured (issue #44). GainExperience's monsterLevel is set to the hero's own
            // current level so its balancing term is neutral — the sim already settled the
            // source-level-balanced XP.
            encounter.EncounterCleared += settledXp => ApplyEncounterXp(player, settledXp);

            WireLoot(encounter, player);
            return encounter;
        }

        /// <summary>
        /// The bag-full retreat trigger's measuring stick (issue #23), or <c>null</c> when the
        /// scene has no inventory wired — the fight then simply never auto-Recalls on a full bag,
        /// the same way it earns no loot without an ItemProvider.
        /// </summary>
        private static IBagGauge BagGauge()
        {
            var provider = InventoryProvider.Instance;
            var bag = provider != null ? provider.Inventory : null;
            return bag != null ? new ContainerBagGauge(bag) : null;
        }

        /// <summary>
        /// Lay the per-kill loot flow over the live Encounter — real bag, real Wallet, the
        /// ItemProvider's catalog / coin tables. A no-op when the scene has no configured
        /// ItemProvider: the fight still runs, just with nothing to earn or lose.
        /// </summary>
        private void WireLoot(EncounterSimulation encounter, LocalPlayer player)
        {
            var itemProvider = ItemProvider.Instance;
            if (itemProvider.Catalog == null || itemProvider.CurrencyDropTable == null
                || itemProvider.CurrencyTypeDistribution == null)
                return;

            _itemGenerator ??= new ItemGenerator(itemProvider.Catalog, new UnityRollSource());
            var coins = new CurrencyDropTableCoinSource(
                itemProvider.CurrencyTypeDistribution, itemProvider.CurrencyDropTable);

            _lootFlow = new LootFlow(encounter, Behaviour, _itemGenerator, coins,
                InventoryProvider.Instance.Inventory, InventoryProvider.Instance.Wallet);

            // The Run accumulates the base-unit coin take so Death's fee reads it (issue #44).
            _lootFlow.CoinsBanked += Run.BankCurrency;
        }

        private static void ApplyEncounterXp(LocalPlayer player, int settledXp)
        {
            if (settledXp > 0)
                player.GainExperience(settledXp, player.CharacterLevel);
        }

        /// <summary>
        /// Send the hero to <paramref name="location"/> — <see cref="RunPhase.InTown"/> →
        /// <see cref="RunPhase.InField"/>. Selects the Location, opens the Run, hands the hero's
        /// regen to the sim for the duration, and — if the standing Corpse is at exactly this
        /// Location — lays it back out (to the bag where it fits, else the ground).
        /// </summary>
        public void Send(LocationConfig location)
        {
            if (location == null) throw new ArgumentNullException(nameof(location));

            SelectedLocation = location;
            var profile = ProfileFor(location);
            Run.Send(profile);

            RecoverCorpseAt(profile);
        }

        /// <summary>End the Run with everything kept (spec story 5). Refused once the hero is down — that ends in <see cref="HandleHeroDeath"/>.</summary>
        public RunResult Recall() => Run.Recall();

        /// <summary>
        /// Close a Run whose hero has been downed — <see cref="RunPhase.InField"/> →
        /// <see cref="RunPhase.InTown"/> with the Death penalty (issue #44 makes it real: the
        /// XP-loss / currency-fee fractions, the bag-to-Corpse hand-off and the XP subtraction;
        /// the revive-at-a-fraction and the final penalty numbers are issue #45). A no-op unless
        /// a Run is live and the hero is actually down, so the driver can call it every frame.
        /// </summary>
        public void HandleHeroDeath()
        {
            if (Run.Phase != RunPhase.InField || !Run.HeroIsDown)
                return;

            var result = Run.HandleDeath(CurrentXpProgress());
            if (result.Outcome == RunOutcome.Died)
                Settlement.Settle(result, SelectedLocation != null ? ProfileFor(SelectedLocation) : null);
        }

        /// <summary>The hero's progress toward its next level, in XP — what the Death penalty's <c>XpLossFraction</c> is taken against.</summary>
        private int CurrentXpProgress()
        {
            var player = CharacterProvider.Instance.Player;
            return player != null ? (int)player.GetResource(StatName.Experience).CurrentValue : 0;
        }

        /// <summary>
        /// Lay the standing Corpse (if it is at <paramref name="profile"/>) back out — to the bag
        /// where it fits, else the ground (or re-buried with no loot flow). The rules are
        /// <see cref="RunSettlement.Recover"/>'s; this only hands it the live ground.
        /// </summary>
        private void RecoverCorpseAt(EncounterProfile profile) => Settlement.Recover(profile, _lootFlow);

        /// <summary>
        /// The one memoized <see cref="EncounterProfile"/> per <see cref="LocationConfig"/> —
        /// <see cref="Corpse"/> keyed by profile <em>reference</em>, so a fresh
        /// <see cref="LocationConfig.ToProfile"/> each call would defeat recovery. Profiles are
        /// immutable, so sharing one across Runs is safe.
        /// </summary>
        private EncounterProfile ProfileFor(LocationConfig location)
        {
            if (!_profiles.TryGetValue(location, out var profile))
                _profiles[location] = profile = location.ToProfile();
            return profile;
        }

        private void OnRunEnded()
        {
            // CONTEXT.md "Drop": a Drop still on the ground when the Run ends is gone, on
            // Recall or Death alike (issue #44). Unsubscribe first, then clear.
            _lootFlow?.Dispose();
            _lootFlow?.ClearGround();
            _lootFlow = null;
        }
    }
}