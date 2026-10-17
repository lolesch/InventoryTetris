using Submodules.Utility.Extensions;
using Submodules.Utility.Services;
using System;
using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The <see cref="ISimulationService"/> of one boot, over a <see cref="ISession"/>. It holds the
    /// mid-loop rules a Run needs and none of its state: the <see cref="RunState"/> FSM and the
    /// loot flow are the World's, the Corpse, the selected Location and the Behaviour Profile the
    /// Hero's. What it does keep is derived and shareable across heroes - the one
    /// <see cref="EncounterProfile"/> per <see cref="LocationConfig"/>, because the Corpse matches a
    /// Location by profile <em>reference</em> and a fresh <see cref="LocationConfig.ToProfile"/> each
    /// call would defeat recovery.
    ///
    /// Builds the <see cref="EncounterSimulation"/> through the factory <see cref="RunState"/> is
    /// handed, binding the hero adapter, the roll source and the Behaviour Profile, and wires the
    /// per-kill loot flow (issue #24) and XP (issue #44) over it. The ADR-0009 Death penalty and the
    /// corpse-recovery rules are <see cref="RunSettlement"/>'s; this binds its ports to the Hero.
    /// The tuning (cast cost, penalty fractions, and the <see cref="EncounterTuning"/> an Encounter plays on)
    /// comes from <see cref="GameConfig"/>, which owns its defaults and its ranges.
    /// </summary>
    public sealed class SimulationService : ISimulationService
    {
        private readonly ISession session;
        private readonly IItemService items;
        private readonly IInventoryService inventory;
        private readonly GameConfig config;
        private readonly RollStreams rolls;
        private readonly ItemGenerator generator;
        private readonly LocationRegistry locations;

        /// <summary>Call sites at the Unity edge read the service as <c>SimulationService.Instance.Run</c>.</summary>
        public static ISimulationService Instance => ServiceLocator.Get<ISimulationService>();

        public SimulationService(ISession session, IItemService items, IInventoryService inventory,
            GameConfig config, RollStreams rolls)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.items = items ?? throw new ArgumentNullException(nameof(items));
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.rolls = rolls ?? throw new ArgumentNullException(nameof(rolls));

            generator = new ItemGenerator(items.Catalog, rolls.Main);
            locations = new LocationRegistry(config.Locations);
        }

        public ILocationRegistry Locations => locations;

        public RunState Run
        {
            get
            {
                var world = session.World;
                return world.Run ??= BuildRun(world);
            }
        }

        public LootFlow LootFlow => session.World.LootFlow;

        public void Send(LocationConfig location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var hero = session.Hero;

            var profile = ProfileFor(location);
            Run.Send(profile);

            // A new Run starts moving, whatever froze the last one.
            SetPaused(false);

            // After the Run accepted it: a refused Send must not move the Location a Death is judged against.
            hero.SelectedLocation = location;

            RecoverCorpseAt(hero, profile);
        }

        public void Relocate(LocationConfig location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var hero = session.Hero;

            var profile = ProfileFor(location);
            Run.Relocate(profile);

            hero.SelectedLocation = location;

            RecoverCorpseAt(hero, profile);
        }

        public bool CorpseLiesAt(LocationConfig location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            var corpse = session.Hero.Corpse;
            return corpse.Exists && corpse.Location == ProfileFor(location);
        }

        public Coordinate ArenaOrigin => config.Tuning.Arena.Origin;

        public event Action CorpseChanged
        {
            add => session.Hero.Corpse.Changed += value;
            remove => session.Hero.Corpse.Changed -= value;
        }

        public event Action<RunResult> RunSettled;

        public RunResult Return()
        {
            var result = Run.Return();
            RunSettled?.Invoke(result);

            return result;
        }

        public bool LeaveField()
        {
            var run = Run;
            if (run.Phase != RunPhase.InField)
                return false;

            // Death first, as in the tick: a hero already down cannot be recalled out of its Death.
            if (run.HeroIsDown)
                HandleHeroDeath(session.Hero, run);
            else
                _ = Return();

            return true;
        }

        public event Action Respawned;

        public void Respawn()
        {
            var hero = session.Hero;
            if (hero.IsDead)
                hero.Heal();

            Respawned?.Invoke();
        }

        // Transient, like the Run it freezes: never saved, and read through IsPaused so a flag left
        // over from another World (a hero load mid-Run) cannot freeze a Run it does not belong to.
        private bool paused;

        public bool IsPaused => paused && Run.Phase == RunPhase.InField;

        public event Action<bool> PausedChanged;

        public void SetPaused(bool value)
        {
            // Pausing needs a Run in the Field; resuming never does, so a stale flag can always be cleared.
            if (value && Run.Phase != RunPhase.InField)
                return;

            if (paused == value)
                return;

            paused = value;
            PausedChanged?.Invoke(value);
        }

        // A menu's freeze, separate from the player's Pause so that releasing it gives the Pause back
        // as found. Transient and read through the phase, like the Pause.
        private bool held;

        public bool IsHeld => held && Run.Phase == RunPhase.InField;

        public void SetHeld(bool value)
        {
            // Holding needs a Run in the Field, so Town has nothing to hold; releasing never does.
            if (value && Run.Phase != RunPhase.InField)
                return;

            held = value;
        }

        public float SimDelta(float deltaSeconds) =>
            IsPaused || IsHeld ? 0f : deltaSeconds * Mathf.Max(0f, session.Hero.Behaviour.SimSpeed);

        public float HeroWalkingSpeed =>
            Mathf.Max(0f, session.Hero.GetStatValue(StatName.MovementSpeed)) * Mathf.Max(0f, config.Tuning.Arena.MovementSpeedScale);

        public void Tick(float deltaSeconds)
        {
            // A frozen Run stands still whole: no regeneration, no Encounter.
            if (IsPaused || IsHeld)
                return;

            var hero = session.Hero;
            var dt = SimDelta(deltaSeconds);

            // Hosted by GameLoop, so this runs after every MonoBehaviour.Update of the frame: a panel
            // that polls the Run in Update sees the previous frame's tick, never a half-stepped one.

            // Regeneration belongs to the living hero at sim speed, in both Town and Field
            // (issue #45). Dead heroes do not regenerate.
            if (!hero.IsDead)
                hero.Regenerate(dt);

            var run = Run;
            if (run.Phase != RunPhase.InField)
                return;

            _ = run.Advance(dt);

            // The death is read after the tick, never inside it: the sim raises its signal and
            // stops, and the RunState transition happens out here.
            if (run.HeroIsDown)
                HandleHeroDeath(hero, run);
        }

        private RunState BuildRun(World world)
        {
            RunState run = null;

            run = new RunState(profile => StartEncounter(world, profile, run),
                new RunPenalty(config.XpLossFraction, config.CurrencyFeeFraction));
            run.RunEnded += () => OnRunEnded(world, run);

            return run;
        }

        /// <summary>
        /// The <see cref="EncounterSimulation"/> factory <see cref="RunState"/> hands its Send -
        /// builds the live Encounter and, on top of it, this Run's loot flow and XP settlement.
        /// </summary>
        private EncounterSimulation StartEncounter(World world, EncounterProfile profile, RunState run)
        {
            // The hero is read when the Encounter starts, not when the Run was built: the Run holds
            // only its World, so nothing here goes stale across a hero change.
            var hero = session.Hero;

            var combatant = new HeroCombatant(hero, Mathf.Max(0f, config.CastCost));

            // A Relocate builds its replacement while the old Encounter's loot flow is still
            // wired - let go of it (and its ground Drops) before the new flow takes its place.
            ReleaseLoot(world);

            // The behaviour goes in by reference, not as a snapshot of its values: the sliders
            // write it live, and Engagement and the Cast threshold are read off it inside the tick
            // (issue #23). The hero arrives to a quiet Location - the
            // first bodies wait one spawn delay.
            var encounter = new EncounterSimulation(combatant, profile, rolls, hero.Behaviour,
                config.Tuning.Sanitised());

            // XP is delivered per kill, independent of the loot flow (issue #44). GainExperience's
            // monsterLevel is the hero's own current level so its balancing term is neutral - the
            // sim already delivers the source-level-balanced XP.
            encounter.XpGained += xp =>
            {
                if (xp > 0)
                    hero.GainExperience(xp, hero.Level);
            };

            var lootFlow = new LootFlow(encounter, hero.Behaviour, generator, items, hero, hero.Wallet,
                world.Ground);

            // The Run accumulates the base-unit coin take so Death's fee reads it (issue #44).
            lootFlow.CoinsBanked += run.BankCurrency;

            // A pick-up that throws mid-tick grounds its Drop; the log is where it surfaces.
            lootFlow.PlacementFailed += (_, exception) => Debug.LogException(exception);

            world.LootFlow = lootFlow;
            return encounter;
        }

        /// <summary>
        /// Close a Run whose hero has been downed - <see cref="RunPhase.InField"/> to
        /// <see cref="RunPhase.InTown"/> with the Death penalty (ADR-0009): the XP-loss and
        /// currency-fee fractions, the bag-to-Corpse hand-off and the XP subtraction.
        /// </summary>
        private void HandleHeroDeath(Hero hero, RunState run)
        {
            var progress = (int)hero.GetResource(StatName.Experience).CurrentValue;
            var result = run.HandleDeath(progress);

            if (result.Outcome != RunOutcome.Died)
                return;

            var fell = hero.SelectedLocation;
            SettlementFor(hero).Settle(result, fell != null ? ProfileFor(fell) : null);

            RunSettled?.Invoke(result);
        }

        /// <summary>
        /// Lay the Hero's Corpse (if it is at <paramref name="profile"/>) back out - to the bag where
        /// it fits, the rest stays buried on it. The rules are <see cref="RunSettlement.Recover"/>'s.
        /// </summary>
        private void RecoverCorpseAt(Hero hero, EncounterProfile profile) =>
            SettlementFor(hero).Recover(profile);

        // Stateless over the Hero's own Corpse, so one is built for each use rather than held across
        // a hero load.
        private static RunSettlement SettlementFor(Hero hero) =>
            new(new ContainerSettlementBag(hero), new PlayerWalletLedger(hero), hero.Corpse);

        // The memo lives on the registry, so a saved Corpse restores onto the profile a Send uses.
        private EncounterProfile ProfileFor(LocationConfig location) => locations.ProfileFor(location);

        private void OnRunEnded(World world, RunState run)
        {
            // GLOSSARY.md "Drop": a Drop still on the ground when the Run ends is gone, on Return or
            // Death alike (issue #44).
            ReleaseLoot(world);

            // The Run is never saved in the Field, so its Kills join the hero's lifetime count here,
            // before RunSettled drives the save. Death counts them too.
            if (run.LastResult is { } result)
                session.Hero.AddKills(result.EnemiesDefeated);

            // Home is never paused or held, Return and Death alike.
            SetPaused(false);
            held = false;

            // Coming home by Return brings the shops new stock; a Death does not.
            if (run.LastResult is { Outcome: RunOutcome.Returned })
                inventory.RestockTownStops();
        }

        /// <summary>Retire the World's live loot flow: unsubscribe first, then clear its ground Drops.</summary>
        private static void ReleaseLoot(World world)
        {
            var lootFlow = world.LootFlow;
            if (lootFlow == null)
                return;

            lootFlow.Dispose();
            lootFlow.ClearGround();
            world.LootFlow = null;
        }
    }
}
