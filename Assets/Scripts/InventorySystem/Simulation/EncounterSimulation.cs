using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

[assembly: InternalsVisibleTo("InventorySystem.Simulation.Tests")]

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The pure Encounter simulation (ADR-0010) — a watchable auto-fight the player only tunes.
    /// A <see cref="CombatClock"/> drives it at a fixed tick; each tick the hero resolves its
    /// two concurrent attacks (a physical <b>Strike</b> on <c>1 / AttackSpeed</c> at the one
    /// sticky target he walks to, a magical area <b>Cast</b> paced by Resource at the densest cluster of enemies in Cast Range),
    /// the enemies Strike back on their own cadences, a per-Location <b>Roster</b> spawns in
    /// (the packed archetype in <b>Packs</b>, the other singly) up to the soft
    /// <see cref="EngagementTarget"/>, and every combatant regenerates once.
    ///
    /// With <see cref="EncounterTuning.DelayFirstSpawn"/> a fresh sim opens
    /// <see cref="SimulationPhase.Arriving"/> — one spawn delay (the Location's
    /// <c>SpawnInterval ± SpawnJitter</c>) passes before its first bodies spawn. An Encounter clears when its Roster is spent and the last
    /// body falls; a short beat; the next Encounter. XP is delivered per kill, the moment the body
    /// falls (<see cref="XpGained"/>) — there is no pot, so nothing carries over a clear and
    /// nothing is forfeited on an exit. The sim runs Encounters endlessly — only the hero going
    /// down, one of the <see cref="HeroBehaviour"/>'s own auto-Recall triggers, or an external
    /// <see cref="Abandon"/> on Recall / Death / Relocate (issue #21) end it. Loot and coins are a
    /// per-kill concern layered on <see cref="EnemyDefeated"/> by issue #24.
    ///
    /// The <see cref="HeroBehaviour"/> is the player's whole input to a Run (issue #23) and is
    /// read live, never snapshotted: the spawn schedule refills toward <see cref="EngagementTarget"/>,
    /// and the hero's Brain (<c>AutoHeroBrain</c>) steers him by it - the <c>CastThreshold</c>
    /// hysteresis latch, the target weights and both auto-Recall triggers, health and bag fill, which
    /// raise <see cref="RecallRequested"/>. Moving the sliders mid-fight therefore changes the fight,
    /// which is the point of them.
    ///
    /// Engine-free and deterministic: every roll (arrival jitter, Roster size, spawn type, Pack
    /// size, jitter, spawn desync) is drawn from the main stream of the injected <see cref="RollStreams"/>, in that
    /// order. The arrival-jitter roll is only drawn with <see cref="EncounterTuning.DelayFirstSpawn"/>.
    /// It never references the hero.
    ///
    /// The sim owns position (spatial-combat spec): the hero starts at the <see cref="Arena"/>'s origin and each
    /// enemy spawns on its edge, walks in on sim time and Strikes only once the hero is within its Strike Range;
    /// the hero's Strike likewise needs its target within <see cref="HeroStrikeRange"/>. Each tick, in order, the
    /// enemies' Brains and the hero's decide (<c>IEnemyBrain</c>, <c>IHeroBrain</c>, from the state at the start of
    /// the tick), the hero moves, the enemies move, and attacks resolve against the new positions. A Recall the
    /// hero's Brain asked for is carried out after the hero-down check. The spawn bearing and stop jitter come from
    /// the movement stream.
    ///
    /// Every hit that lands (the hero's Strike and Cast, each enemy Strike) rolls its damage spread from the hit
    /// stream and is announced by <see cref="HitLanded"/> with the amount the target actually lost (issue #211).
    /// </summary>
    public sealed class EncounterSimulation
    {
        private readonly IHeroCombatant _hero;
        private readonly EncounterProfile _profile;
        private readonly IRollSource _rolls;
        private readonly HeroBehaviour _behaviour;
        private readonly IBagGauge _bag;
        private readonly EncounterTuning _tuning;
        private readonly Arena _arena;
        private readonly IRollSource _movementRolls;
        private readonly IRollSource _hitRolls;
        private readonly CombatClock _clock;
        private readonly List<Enemy> _enemies = new();
        private readonly Func<Enemy, IEnemyBrain> _enemyBrains;
        private readonly IHeroBrain _heroBrain;
        private readonly List<Intent> _heroIntents = new(); // reused buffer - this tick's hero decisions
        private readonly List<Intent> _asked = new(); // reused buffer - one Brain's answer
        private readonly List<EnemyIntent> _enemyIntents = new(); // reused buffer - this tick's decisions, in enemy order

        private readonly struct EnemyIntent
        {
            public EnemyIntent(Enemy actor, Intent intent)
            {
                Actor = actor;
                Intent = intent;
            }

            public Enemy Actor { get; }
            public Intent Intent { get; }
        }

        private int _rosterBrute;
        private int _rosterSkirmisher;
        private int _spawnedBrute;
        private int _spawnedSkirmisher;
        private int _nextSpawnIndex;

        private Enemy _heroTarget; // the target of the hero's latest Strike or Approach Intent; may have fallen since
        private bool _heroStrikeReady; // this tick's cadence flags, told to the hero's Brain and kept for carrying it out
        private bool _heroCastBeatOpen;
        private bool _heroAskedRecall;

        private float _spawnTimer;
        private float _strikeTimer;
        private float _castTimer;
        private float _beatEndsAt;
        private float _arrivalEndsAt;
        private float _endedAt;

        /// <param name="behaviour">
        /// The player's live steering (issue #23) — Engagement, the Cast threshold and both
        /// auto-Recall triggers. Held by reference, not copied: a slider moved mid-fight lands
        /// on the next tick.
        /// </param>
        /// <param name="bag">
        /// How full the hero's bag is, for <see cref="HeroBehaviour.ShouldRecallForBagFull"/>.
        /// Optional — an Encounter built without one never fires the bag-full auto-Recall, which
        /// is what a fight with no storage wired to it should do.
        /// </param>
        /// <param name="rolls">
        /// Every roll the Encounter draws - the main stream, the movement stream (spawn bearing, stop jitter) and
        /// the hit stream (the damage spread). The streams stay separate so a seeded outcome never reorders: see
        /// <see cref="RollStreams"/>.
        /// </param>
        public EncounterSimulation(
            IHeroCombatant hero,
            EncounterProfile profile,
            RollStreams rolls,
            HeroBehaviour behaviour,
            EncounterTuning tuning = null,
            IBagGauge bag = null)
            : this(hero, profile, rolls, behaviour, tuning, bag, null, null)
        {
        }

        /// <param name="enemyBrains">
        /// Builds the Brain of each enemy as it spawns; null gives every enemy the <see cref="ChaseAndStrikeBrain"/>.
        /// A test scripts the enemies through it.
        /// </param>
        /// <param name="heroBrain">
        /// Decides the hero's moves; null gives him the <see cref="AutoHeroBrain"/>, which carries out the
        /// <paramref name="behaviour"/>. A test scripts the hero through it.
        /// </param>
        internal EncounterSimulation(
            IHeroCombatant hero,
            EncounterProfile profile,
            RollStreams rolls,
            HeroBehaviour behaviour,
            EncounterTuning tuning,
            IBagGauge bag,
            Func<Enemy, IEnemyBrain> enemyBrains,
            IHeroBrain heroBrain = null)
        {
            _enemyBrains = enemyBrains;
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rolls = rolls?.Main ?? throw new ArgumentNullException(nameof(rolls));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _bag = bag;
            _movementRolls = rolls.Movement;
            _hitRolls = rolls.Hit;

            _tuning = tuning ?? new EncounterTuning();
            _tuning.Validate();
            _heroBrain = heroBrain ?? new AutoHeroBrain(_behaviour, _bag, _tuning.HeroTargeting);
            _arena = new Arena(_tuning.Arena);

            _clock = new CombatClock(_tuning.Tick, _tuning.MaxTicksPerAdvance);
            _clock.OnTick += Step;

            if (_tuning.DelayFirstSpawn)
            {
                _arrivalEndsAt = NextSpawnDelay();
                Phase = SimulationPhase.Arriving;
            }
            else
                BeginEncounter();
        }

        /// <summary>One spawn delay — the Location's interval, jittered. The arrival wait and the spawn schedule share it.</summary>
        private float NextSpawnDelay() =>
            _profile.SpawnInterval + ((float)_rolls.Next() * 2f - 1f) * _profile.SpawnJitter;

        /// <summary>
        /// The soft count of enemies the fight refills toward — <see cref="HeroBehaviour.Engagement"/>
        /// read live, so raising it mid-Run refills toward the new target on the next spawn tick.
        /// Not a ceiling: a Pack that arrives while the count is below the target overshoots it.
        ///
        /// Clamped to at least 1 rather than validated: the behaviour is a live value a slider
        /// writes at any moment, so a zero has to be absorbed, not thrown mid-fight.
        /// </summary>
        public int EngagementTarget => Math.Max(1, _behaviour.Engagement);

        /// <summary>The Location this Encounter is fought at — its loot table and source level (issue #24).</summary>
        public EncounterProfile Profile => _profile;

        /// <summary>The hero fighting this Encounter — its live magic find and item quantity (issue #24).</summary>
        public IHeroCombatant Hero => _hero;

        public SimulationPhase Phase { get; private set; } = SimulationPhase.Fighting;

        /// <summary>Whether the first enemies are still on their way — the sim is <see cref="SimulationPhase.Arriving"/>.</summary>
        public bool IsArriving => Phase == SimulationPhase.Arriving;

        /// <summary>The live enemies, in spawn order. Read-only — the sim owns their lifetime.</summary>
        public IReadOnlyList<Enemy> Enemies => _enemies;

        public int AliveEnemyCount => _enemies.Count;

        /// <summary>The arena the fight takes place on - its origin, radius and the unarmed Strike Range.</summary>
        public ArenaTuning Arena => _arena.Tuning;

        /// <summary>
        /// Where the hero stands on the arena. He starts at the origin, walks to his target on sim time and walks
        /// back when nothing lives; the arena only reads it.
        /// </summary>
        public Coordinate HeroPosition => _arena.HeroPosition;

        /// <summary>How far from <see cref="HeroPosition"/> an enemy may stand to be aimed at by the Cast: the Cast definition's range.</summary>
        public float CastRange => _tuning.Cast.Range;

        /// <summary>
        /// How far from <see cref="HeroPosition"/> the hero's Strike reaches: his weapon type's range, or the
        /// arena's unarmed range while he wields none. Read live, so re-gearing changes it on the spot.
        /// </summary>
        public float HeroStrikeRange => _hero.WeaponStrikeRange ?? _arena.Tuning.HeroStrikeRange;

        /// <summary>
        /// The enemy the hero is fighting - the target of his latest Strike or Approach Intent (issues #182, #209) -
        /// or null before the first tick and whenever he named none, such as walking home with no one left. It may be
        /// out of reach, the hero then walking to it, and it may have fallen since the tick that named it. The Cast's
        /// targets are not this.
        /// </summary>
        public Enemy HeroTarget => _heroTarget;

        /// <summary>1-based index of the Encounter currently building or being fought.</summary>
        public int CurrentEncounter { get; private set; }

        public int EncountersCleared { get; private set; }

        /// <summary>Every enemy that has fallen across the whole sequence.</summary>
        public int EnemiesDefeated { get; private set; }

        /// <summary>
        /// Tick-quantised elapsed simulation time, summed over Encounters and beats. Frozen at
        /// the tick the sim ended, so a coarse <see cref="Advance"/> that overshoots the hero's
        /// death does not inflate it.
        /// </summary>
        public float Duration => Phase == SimulationPhase.Ended ? _endedAt : _clock.ElapsedTime;

        /// <summary>Sum of the XP every kill has delivered so far — already handed over via <see cref="XpGained"/>.</summary>
        public int SettledXp { get; private set; }

        /// <summary>
        /// Raised as each body arrives, once it is in <see cref="Enemies"/> and fully built — the
        /// symmetric pair to <see cref="EnemyDefeated"/> (issue #94). Raised from the one spawn
        /// path, so a later Encounter's initial batch needs no special case. The <i>first</i>
        /// Encounter's initial batch is spawned inside the constructor, before anyone can
        /// subscribe: a listener attaching later seeds itself from <see cref="Enemies"/>.
        /// </summary>
        public event Action<Enemy> EnemySpawned;

        /// <summary>Raised as each body falls — the per-kill seam issue #24 rolls loot and coins on.</summary>
        public event Action<Enemy> EnemyDefeated;

        /// <summary>
        /// Raised as each body falls, carrying the whole-number XP that kill is worth — the hero's
        /// level-balanced share, rounded per kill. Not raised for a kill worth nothing. Raised
        /// before <see cref="EnemyDefeated"/>.
        /// </summary>
        public event Action<int> XpGained;

        /// <summary>Raised on an Encounter clear — the Roster is spent and the last body is down.</summary>
        public event Action EncounterCleared;

        /// <summary>Raised the tick the hero's health reaches 0.</summary>
        public event Action HeroDowned;

        /// <summary>
        /// Raised the tick one of <see cref="HeroBehaviour"/>'s auto-Recall triggers fires —
        /// health at or below <c>RetreatHealthFraction</c>, or the bag filled to
        /// <c>RecallBagFillFraction</c> (issue #23). The fight is already stopped and its
        /// stopped by the time this raises, exactly as a manual Recall would
        /// leave it; the engine-side driver turns the signal into <see cref="RunState.Recall"/>
        /// so the Run ends down the one path, with everything kept.
        ///
        /// Raised at most once per Encounter — the sim has <see cref="SimulationPhase.Ended"/>
        /// and stops stepping, so a driver that ignores it simply leaves a stopped fight.
        /// </summary>
        public event Action RecallRequested;

        /// <summary>Raised each tick the hero lands a physical Strike — the Ability hotbar's flash (issue #62).</summary>
        public event Action HeroStriked;

        /// <summary>Raised each tick the hero commits a magical Cast — the Ability hotbar's flash (issue #62).</summary>
        public event Action HeroCast;

        /// <summary>
        /// Raised for every hit that lands - the hero's Strike and Cast and each enemy Strike (issue #211) - with its
        /// dealer, target, damage type, raw amount and the amount actually lost. Raised before the Strike/Cast
        /// flash events and before a kill is processed, so the target is still in <see cref="Enemies"/>.
        /// </summary>
        public event Action<HitEvent> HitLanded;

        /// <summary>
        /// Bank <paramref name="deltaSeconds"/> of real time and run every whole tick now due
        /// (0..N). Returns the number of ticks run. A no-op once the sim has
        /// <see cref="SimulationPhase.Ended"/>.
        /// </summary>
        public int Advance(float deltaSeconds)
        {
            if (Phase == SimulationPhase.Ended) return 0;
            return _clock.Advance(deltaSeconds);
        }

        /// <summary>
        /// End the fight — the Run was Recalled, Relocated or the hero Died (issue #21). XP is
        /// delivered per kill, so there is nothing in flight to settle or lose. Idempotent.
        /// </summary>
        public void Abandon()
        {
            if (Phase == SimulationPhase.Ended) return;
            End();
        }

        private void End()
        {
            _endedAt = _clock.ElapsedTime;
            Phase = SimulationPhase.Ended;
        }

        private void Step()
        {
            if (Phase == SimulationPhase.Ended) return;

            var dt = _tuning.Tick;

            if (Phase == SimulationPhase.Arriving)
            {
                if (_clock.ElapsedTime >= _arrivalEndsAt)
                    BeginEncounter();
                return;
            }

            if (Phase == SimulationPhase.Beat)
            {
                // Nothing lives: his Brain sends him home through the quiet. The cadences stand still.
                DecideHero(dt, fighting: false);
                WalkHero(dt);
                if (EndOnRecall())
                    return;

                if (_clock.ElapsedTime >= _beatEndsAt)
                    BeginEncounter();
                return;
            }

            RunSpawnSchedule(dt);

            for (var i = 0; i < _enemies.Count; i++)
                _enemies[i].Regenerate(dt);

            // Positions advance here and nowhere else, on the tick, so they scale with sim speed and freeze at
            // zero. In a fixed order: every Brain decides from the state at the start of the tick, the hero
            // moves, the enemies move, and only then does anyone attack, against this tick's positions.
            DecideEnemies(dt);
            DecideHero(dt, fighting: true);
            WalkHero(dt);
            WalkEnemies(dt);

            ExecuteHeroStrike();
            ExecuteHeroCast();
            ExecuteEnemyStrikes();

            if (_hero.IsDown)
            {
                End();
                HeroDowned?.Invoke();
                return;
            }

            // Checked after the death test, so a tick that both asks for a Recall and kills the hero is a
            // Death, not a Recall — the penalty is not dodgeable by the trigger racing it.
            if (EndOnRecall())
                return;

            if (RosterSpent && _enemies.Count == 0)
                ClearEncounter();
        }

        /// <summary>End the fight if the hero's Brain asked for a Recall this tick, as a manual Recall would leave it.</summary>
        private bool EndOnRecall()
        {
            if (!_heroAskedRecall)
                return false;

            End();
            RecallRequested?.Invoke();
            return true;
        }

        // ─── spawning ────────────────────────────────────────────────────────

        private bool RosterSpent => _spawnedBrute >= _rosterBrute && _spawnedSkirmisher >= _rosterSkirmisher;

        private int RosterRemaining(EnemyArchetype archetype) => RosterOf(archetype) - SpawnedOf(archetype);

        private void BeginEncounter()
        {
            CurrentEncounter++;
            _enemies.Clear();
            _heroTarget = null;
            _spawnedBrute = 0;
            _spawnedSkirmisher = 0;
            _spawnTimer = _profile.SpawnInterval;
            // The hero's Strike / Cast timers carry across the beat — each fight is not a fresh
            // cadence, it is the same hero swinging without pause (matches the /prototype).

            _rosterBrute = _profile.RosterBrute.Roll(_rolls);
            _rosterSkirmisher = _profile.RosterSkirmisher.Roll(_rolls);

            var left = _profile.InitialSpawn;
            foreach (var archetype in new[] { _profile.Packed, EnemyArchetypes.Other(_profile.Packed) })
                while (left > 0 && SpawnedOf(archetype) < RosterOf(archetype))
                {
                    Spawn(archetype);
                    left--;
                }

            Phase = SimulationPhase.Fighting;
        }

        private int RosterOf(EnemyArchetype a) => a == EnemyArchetype.Brute ? _rosterBrute : _rosterSkirmisher;
        private int SpawnedOf(EnemyArchetype a) => a == EnemyArchetype.Brute ? _spawnedBrute : _spawnedSkirmisher;

        private void RunSpawnSchedule(float dt)
        {
            if (RosterSpent) return;

            _spawnTimer -= dt;
            if (_spawnTimer > 0f || _enemies.Count >= EngagementTarget) return;

            var remBrute = RosterRemaining(EnemyArchetype.Brute);
            var remSkirmisher = RosterRemaining(EnemyArchetype.Skirmisher);

            EnemyArchetype type;
            if (remBrute > 0 && remSkirmisher > 0)
                type = _rolls.Next() < _profile.PackedSpawnWeight ? _profile.Packed : EnemyArchetypes.Other(_profile.Packed);
            else
                type = remBrute > 0 ? EnemyArchetype.Brute : EnemyArchetype.Skirmisher;

            var batch = type == _profile.Packed ? _profile.PackBatch.Roll(_rolls) : 1;
            batch = Math.Min(batch, RosterRemaining(type));
            for (var i = 0; i < batch; i++)
                Spawn(type);

            _spawnTimer = NextSpawnDelay();
        }

        private void Spawn(EnemyArchetype archetype)
        {
            var enemy = new Enemy(archetype, _profile.SourceLevel)
            {
                SpawnIndex = _nextSpawnIndex++,
                // desync so a Pack does not strike in lockstep
                StrikeTimer = (float)_rolls.Next() * (1f / EnemyArchetypes.Of(archetype).AttackSpeed),
            };
            enemy.Brain = _enemyBrains?.Invoke(enemy) ?? ChaseAndStrikeBrain.Instance;
            var bearingRoll = (float)_movementRolls.Next();
            _arena.PlaceSpawn(enemy, _enemies, bearingRoll, stopRoll: (float)_movementRolls.Next());
            _enemies.Add(enemy);

            if (archetype == EnemyArchetype.Brute) _spawnedBrute++;
            else _spawnedSkirmisher++;

            EnemySpawned?.Invoke(enemy);
        }

        // ─── the hero's Brain ────────────────────────────────────────────────

        /// <summary>
        /// Bank this tick's time on the hero's two cadences (only while fighting; the quiet between Encounters stands
        /// still) and ask his Brain what he wants, all from the state at the start of the tick. The Brain is told
        /// when the Strike is ready and the Cast beat is open; whether the ask is allowed is for the carrying out.
        /// </summary>
        private void DecideHero(float dt, bool fighting)
        {
            _heroStrikeReady = false;
            _heroCastBeatOpen = false;
            if (fighting)
            {
                _strikeTimer += dt;
                _heroStrikeReady = !(_strikeTimer < 1f / _hero.AttackSpeed);
                _castTimer += dt;
                _heroCastBeatOpen = !(_castTimer < _tuning.CastCadence);
            }

            _heroIntents.Clear();
            _heroBrain.Decide(new HeroView(_arena, _hero, _enemies, HeroStrikeRange, _heroStrikeReady, _heroCastBeatOpen), _heroIntents);

            _heroTarget = null;
            _heroAskedRecall = false;
            for (var i = 0; i < _heroIntents.Count; i++)
            {
                var intent = _heroIntents[i];
                if (intent.Kind == IntentKind.Recall)
                    _heroAskedRecall = true;
                else if ((intent.Kind == IntentKind.Strike || intent.Kind == IntentKind.Approach) && intent.Target is Enemy enemy)
                    _heroTarget = enemy;
            }
        }

        /// <summary>
        /// Carry out the hero's first walking Intent - one step a tick, whatever else he asked: an Approach goes
        /// toward its target and stops within his Strike Range, a Walk stops on its point.
        /// </summary>
        private void WalkHero(float dt)
        {
            for (var i = 0; i < _heroIntents.Count; i++)
            {
                var intent = _heroIntents[i];
                if (intent.Kind == IntentKind.Walk)
                    _arena.WalkHero(_hero.MovementSpeed, dt, intent.Point, 0f);
                else if (intent.Kind == IntentKind.Approach && intent.Target is Enemy enemy)
                    _arena.WalkHero(_hero.MovementSpeed, dt, enemy.Position, HeroStrikeRange);
                else
                    continue;
                return;
            }
        }

        // ─── the hero's two attacks ──────────────────────────────────────────

        private bool HeroAsked(IntentKind kind, out Intent asked)
        {
            for (var i = 0; i < _heroIntents.Count; i++)
                if (_heroIntents[i].Kind == kind)
                {
                    asked = _heroIntents[i];
                    return true;
                }

            asked = default;
            return false;
        }

        /// <summary>
        /// Carry out the hero's Strike Intent against this tick's positions. It lands only when the swing is ready
        /// and the target is alive and in reach; one refused costs nothing. A swing that is ready but did not land
        /// waits at one interval and banks no burst, so arrival is one Strike.
        /// </summary>
        private void ExecuteHeroStrike()
        {
            if (!_heroStrikeReady || !HeroAsked(IntentKind.Strike, out var asked)) return;

            var interval = 1f / _hero.AttackSpeed;
            var target = asked.Target as Enemy;
            if (target == null || target.IsDown || !_arena.InRangeOfHero(target.Position, HeroStrikeRange))
            {
                // His target is still out of reach (he is walking to it, or it is walking in).
                _strikeTimer = interval;
                return;
            }

            _strikeTimer = Math.Min(_strikeTimer - interval, interval); // at most one Strike per tick

            Land(_hero, target, DamageType.PhysicalDamage, _hero.PhysicalDamage);
            HeroStriked?.Invoke();
            if (target.IsDown) Defeat(target);
        }

        private bool CanAffordCast(float resource) => resource >= _hero.CastCost;

        /// <summary>
        /// Carry out the hero's Cast Intent. The cadence is the Encounter's: the beat opens once per
        /// <see cref="EncounterTuning.CastCadence"/> whether or not he casts, and a Cast asked off the beat is refused.
        /// </summary>
        private void ExecuteHeroCast()
        {
            if (!_heroCastBeatOpen) return;

            _castTimer = Math.Min(_castTimer - _tuning.CastCadence, _tuning.CastCadence);

            if (!HeroAsked(IntentKind.Cast, out _) || !CanAffordCast(_hero.Resource)) return;

            // No one to aim at, or a shape that would catch nobody: the Cast does not fire and spends nothing.
            var aim = _arena.DensestCluster(_enemies, _tuning.Cast);
            if (aim == null) return;

            // The Cast that leaves too little for another takes the whole remainder, so a run always ends on 0.
            var drains = !CanAffordCast(_hero.Resource - _hero.CastCost);
            _hero.SpendResource(drains ? _hero.Resource : _hero.CastCost);
            if (drains) _behaviour.ReleaseCastingRun();
            HeroCast?.Invoke();

            var targets = _arena.InShape(_enemies, _tuning.Cast, aim);
            for (var i = 0; i < targets.Count; i++)
            {
                Land(_hero, targets[i], DamageType.MagicalDamage, _hero.MagicalDamage);
                if (targets[i].IsDown) Defeat(targets[i]);
            }
        }

        /// <summary>
        /// Deal one hit and announce it: the base damage takes its spread (one roll from the hit stream, drawn for
        /// every hit so the stream's position never depends on the tuning), the target mitigates it by
        /// <paramref name="type"/> and reports what it lost. Not announced when the target was already down -
        /// nothing landed.
        /// </summary>
        private void Land(ICombatant dealer, ICombatant target, DamageType type, float baseDamage)
        {
            var rawAmount = baseDamage * (1f + ((float)_hitRolls.Next() * 2f - 1f) * _tuning.DamageSpread);
            var wasDown = target.IsDown;
            var lost = type == DamageType.MagicalDamage ? target.ReceiveMagical(rawAmount) : target.ReceivePhysical(rawAmount);
            if (wasDown)
                return;

            var heroIsTarget = ReferenceEquals(target, _hero);
            var reference = heroIsTarget
                ? DamageReference.OfEnemies(_profile, type, _tuning.DamageSpread)
                : DamageReference.OfHero(_hero, type, _tuning.DamageSpread);
            var place = target is Enemy enemy ? enemy.Position : HeroPosition;
            HitLanded?.Invoke(new HitEvent(dealer, target, type, rawAmount, lost, reference, place));
        }

        // ─── the enemies' Brains ─────────────────────────────────────────────

        /// <summary>
        /// Bank this tick's time on each enemy's Strike cadence and ask its Brain what it wants, all from the state at
        /// the start of the tick: nothing has moved or struck yet, so no Brain sees another's move.
        /// </summary>
        private void DecideEnemies(float dt)
        {
            _enemyIntents.Clear();
            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                enemy.StrikeTimer += dt;

                _asked.Clear();
                enemy.Brain.Decide(new EnemyView(_arena, enemy, _hero, StrikeIsReady(enemy)), _asked);
                for (var j = 0; j < _asked.Count; j++)
                    _enemyIntents.Add(new EnemyIntent(enemy, _asked[j]));
            }
        }

        private static bool StrikeIsReady(Enemy enemy) => !(enemy.StrikeTimer < 1f / enemy.AttackSpeed);

        /// <summary>Carry out the enemies' Approach Intents; only the hero can be approached.</summary>
        private void WalkEnemies(float dt)
        {
            for (var i = 0; i < _enemyIntents.Count; i++)
            {
                var (actor, intent) = (_enemyIntents[i].Actor, _enemyIntents[i].Intent);
                if (intent.Kind == IntentKind.Approach && ReferenceEquals(intent.Target, _hero))
                    _arena.WalkEnemy(actor, dt);
            }
        }

        /// <summary>
        /// Carry out the enemies' Strike Intents against this tick's positions. A Strike lands only when the swing is
        /// ready and the hero is in reach; one refused costs nothing. A swing that is ready but did not land waits at
        /// one interval and banks no burst, so arrival is one Strike.
        /// </summary>
        private void ExecuteEnemyStrikes()
        {
            for (var i = 0; i < _enemyIntents.Count; i++)
            {
                var (enemy, intent) = (_enemyIntents[i].Actor, _enemyIntents[i].Intent);
                if (intent.Kind != IntentKind.Strike || enemy.IsDown) continue;
                if (!ReferenceEquals(intent.Target, _hero) || !StrikeIsReady(enemy)) continue;
                if (!_arena.InRangeOfHero(enemy.Position, enemy.StrikeRange)) continue;

                var interval = 1f / enemy.AttackSpeed;
                enemy.StrikeTimer = Math.Min(enemy.StrikeTimer - interval, interval);
                Land(enemy, _hero, enemy.StrikeDamageType, enemy.StrikeDamage);
            }

            for (var i = 0; i < _enemies.Count; i++)
                _enemies[i].StrikeTimer = Math.Min(_enemies[i].StrikeTimer, 1f / _enemies[i].AttackSpeed);
        }

        // ─── kills, clears, XP ───────────────────────────────────────────────

        private void Defeat(Enemy enemy)
        {
            _enemies.Remove(enemy);
            EnemiesDefeated++;

            var balanced = enemy.Xp * (1f + (_profile.SourceLevel - _hero.Level) / 100f);
            var xp = (int)Math.Round(Math.Max(0f, balanced), MidpointRounding.AwayFromZero);
            if (xp > 0)
            {
                SettledXp += xp;
                XpGained?.Invoke(xp);
            }

            EnemyDefeated?.Invoke(enemy);
        }

        private void ClearEncounter()
        {
            EncountersCleared++;

            Phase = SimulationPhase.Beat;
            _beatEndsAt = _clock.ElapsedTime + _tuning.Beat;

            EncounterCleared?.Invoke();
        }
    }
}
