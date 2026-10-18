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
    /// The pure Encounter simulation (ADR-0010): a watchable auto-fight the player only tunes, engine-free and
    /// deterministic from its injected <see cref="RollStreams"/>. A fixed-tick clock drives it; the Brains decide, the
    /// Encounter carries out what the rules allow (ADR-0020), and the sim owns position (ADR-0018).
    /// </summary>
    public sealed class EncounterSimulation
    {
        private readonly IHeroCombatant _hero;
        private readonly EncounterProfile _profile;
        private readonly RollStreams _streams;
        private readonly HeroBehaviour _behaviour;
        private readonly EncounterTuning _tuning;
        private readonly Arena _arena;
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

        private Enemy _heroTarget; // the target of the hero's latest Strike or Approach Intent
        private bool _heroStrikeReady; // this tick's cadence flags, told to the hero's Brain and kept for carrying it out
        private bool _heroCastBeatOpen;

        private float _spawnTimer;
        private float _strikeTimer;
        private float _castTimer;
        private float _beatEndsAt;
        private float _arrivalEndsAt;
        private float _endedAt;

        /// <param name="behaviour">
        /// The player's live steering (issue #23) — Engagement and the Cast threshold. Held by
        /// reference, not copied: a slider moved mid-fight lands on the next tick.
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
            EncounterTuning tuning = null)
            : this(hero, profile, rolls, behaviour, tuning, null, null)
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
            Func<Enemy, IEnemyBrain> enemyBrains,
            IHeroBrain heroBrain = null)
        {
            _enemyBrains = enemyBrains;
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _streams = rolls ?? throw new ArgumentNullException(nameof(rolls));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));

            _tuning = tuning ?? new EncounterTuning();
            _tuning.Validate();
            Population = _profile.Population.Roll(_streams.Main);
            _heroBrain = heroBrain ?? new AutoHeroBrain(_behaviour, _tuning.HeroTargeting);
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
            _profile.SpawnInterval + ((float)_streams.Main.Next() * 2f - 1f) * _profile.SpawnJitter;

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
        /// out of reach, the hero then walking to it. One that falls is swapped for the Brain's next pick at once. The
        /// Cast's targets are not this.
        /// </summary>
        public Enemy HeroTarget => _heroTarget;

        /// <summary>1-based index of the Encounter currently building or being fought.</summary>
        public int CurrentEncounter { get; private set; }

        /// <summary>How many Encounters this Send fields, rolled once from the Location's range.</summary>
        public int Population { get; }

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

        /// <summary>Raised once, as the last Encounter of the Population clears and the sim enters <see cref="SimulationPhase.Complete"/>.</summary>
        public event Action LocationCompleted;

        /// <summary>Raised the tick the hero's health reaches 0.</summary>
        public event Action HeroDowned;

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
        /// <see cref="SimulationPhase.Ended"/>, and once <see cref="SimulationPhase.Complete"/>, where time stands still.
        /// </summary>
        public int Advance(float deltaSeconds)
        {
            if (Phase == SimulationPhase.Ended || Phase == SimulationPhase.Complete) return 0;
            return _clock.Advance(deltaSeconds);
        }

        /// <summary>
        /// End the fight — the Run was Returned, Relocated or the hero Died (issue #21). XP is
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

            RetargetIfFallen();

            if (RosterSpent && _enemies.Count == 0)
                ClearEncounter();
        }

        /// <summary>The Brain decides once a tick, so a target killed this tick is swapped for its next pick now, not left a corpse until then.</summary>
        private void RetargetIfFallen()
        {
            if (_heroTarget == null || !_heroTarget.IsDown)
                return;

            _heroTarget = _heroBrain.PeekTarget(new HeroView(_arena, _hero, _enemies, HeroStrikeRange, _heroStrikeReady, _heroCastBeatOpen));
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

            _rosterBrute = _profile.RosterBrute.Roll(_streams.Main);
            _rosterSkirmisher = _profile.RosterSkirmisher.Roll(_streams.Main);

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
                type = _streams.Main.Next() < _profile.PackedSpawnWeight ? _profile.Packed : EnemyArchetypes.Other(_profile.Packed);
            else
                type = remBrute > 0 ? EnemyArchetype.Brute : EnemyArchetype.Skirmisher;

            var batch = type == _profile.Packed ? _profile.PackBatch.Roll(_streams.Main) : 1;
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
                StrikeTimer = (float)_streams.Main.Next() * (1f / EnemyArchetypes.Of(archetype).AttackSpeed),
            };
            enemy.Brain = _enemyBrains?.Invoke(enemy) ?? ChaseAndStrikeBrain.Instance;
            _arena.PlaceSpawn(enemy, _enemies, (float)_streams.Movement.Next(), (float)_streams.Movement.Next());
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
                _heroStrikeReady = IsDue(_strikeTimer, 1f / _hero.AttackSpeed);
                _castTimer += dt;
                _heroCastBeatOpen = IsDue(_castTimer, _tuning.CastCadence);
            }

            _heroIntents.Clear();
            _heroBrain.Decide(new HeroView(_arena, _hero, _enemies, HeroStrikeRange, _heroStrikeReady, _heroCastBeatOpen), _heroIntents);

            _heroTarget = null;
            for (var i = 0; i < _heroIntents.Count; i++)
            {
                var intent = _heroIntents[i];
                if ((intent.Kind == IntentKind.Strike || intent.Kind == IntentKind.Approach) && intent.Target is Enemy enemy)
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
        /// and the target is alive and in reach; one refused costs nothing.
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

            _strikeTimer = Bank(_strikeTimer, interval);

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

            _castTimer = Bank(_castTimer, _tuning.CastCadence);

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
        /// Deal one hit and announce it. One hit-stream roll is drawn per hit, so the stream's position never
        /// depends on the tuning; nothing is announced when the target was already down.
        /// </summary>
        private void Land(ICombatant dealer, ICombatant target, DamageType type, float baseDamage)
        {
            var rawAmount = baseDamage * (1f + ((float)_streams.Hit.Next() * 2f - 1f) * _tuning.DamageSpread);
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

        private static bool StrikeIsReady(Enemy enemy) => IsDue(enemy.StrikeTimer, 1f / enemy.AttackSpeed);

        private static bool IsDue(float timer, float interval) => !(timer < interval);

        /// <summary>
        /// The timer after a due swing: one interval paid, at most one more kept, so a tick never fires twice.
        /// A swing that is ready but did not land waits at one interval and banks no burst, so arrival is one Strike.
        /// </summary>
        private static float Bank(float timer, float interval) => Math.Min(timer - interval, interval);

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
        /// ready and the hero is in reach; one refused costs nothing.
        /// </summary>
        private void ExecuteEnemyStrikes()
        {
            for (var i = 0; i < _enemyIntents.Count; i++)
            {
                var (enemy, intent) = (_enemyIntents[i].Actor, _enemyIntents[i].Intent);
                if (intent.Kind != IntentKind.Strike || enemy.IsDown) continue;
                if (!ReferenceEquals(intent.Target, _hero) || !StrikeIsReady(enemy)) continue;
                if (!_arena.InRangeOfHero(enemy.Position, enemy.StrikeRange)) continue;

                enemy.StrikeTimer = Bank(enemy.StrikeTimer, 1f / enemy.AttackSpeed);
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

            var complete = EncountersCleared >= Population;
            Phase = complete ? SimulationPhase.Complete : SimulationPhase.Beat;
            _beatEndsAt = _clock.ElapsedTime + _tuning.Beat;

            EncounterCleared?.Invoke();
            if (complete)
                LocationCompleted?.Invoke();
        }
    }
}
