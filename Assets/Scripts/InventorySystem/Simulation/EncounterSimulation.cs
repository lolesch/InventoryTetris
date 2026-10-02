using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ToolSmiths.InventorySystem.Items;

[assembly: InternalsVisibleTo("InventorySystem.Simulation.Tests")]

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The pure Encounter simulation (ADR-0010) — a watchable auto-fight the player only tunes.
    /// A <see cref="CombatClock"/> drives it at a fixed tick; each tick the hero resolves its
    /// two concurrent attacks (a physical <b>Strike</b> on <c>1 / AttackSpeed</c> at the single
    /// lowest-HP enemy, a magical area <b>Cast</b> paced by Resource at the highest-HP enemies),
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
    /// read live, never snapshotted, at four points: the spawn schedule refills toward
    /// <see cref="EngagementTarget"/>, <see cref="ResolveCast"/> is gated by the
    /// <c>CastThreshold</c> hysteresis latch, and both auto-Recall triggers — health and bag
    /// fill — are checked once a tick, raising <see cref="RecallRequested"/>. Moving the sliders
    /// mid-fight therefore changes the fight, which is the point of them.
    ///
    /// Engine-free and deterministic: every roll (arrival jitter, Roster size, spawn type, Pack
    /// size, jitter, spawn desync) is drawn from the injected <see cref="IRollSource"/>, in that
    /// order. The arrival-jitter roll is only drawn with <see cref="EncounterTuning.DelayFirstSpawn"/>.
    /// It never references <c>BaseCharacter</c>.
    /// </summary>
    public sealed class EncounterSimulation
    {
        private readonly IHeroCombatant _hero;
        private readonly EncounterProfile _profile;
        private readonly IRollSource _rolls;
        private readonly HeroBehaviour _behaviour;
        private readonly IBagGauge _bag;
        private readonly EncounterTuning _tuning;
        private readonly CombatClock _clock;
        private readonly List<Enemy> _enemies = new();
        private readonly List<Enemy> _castTargets = new(); // reused buffer — the Cast runs on a cadence

        private int _rosterBrute;
        private int _rosterSkirmisher;
        private int _spawnedBrute;
        private int _spawnedSkirmisher;
        private int _nextSpawnIndex;

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
        public EncounterSimulation(
            IHeroCombatant hero,
            EncounterProfile profile,
            IRollSource rolls,
            HeroBehaviour behaviour,
            EncounterTuning tuning = null,
            IBagGauge bag = null)
        {
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rolls = rolls ?? throw new ArgumentNullException(nameof(rolls));
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _bag = bag;

            _tuning = tuning ?? new EncounterTuning();
            _tuning.Validate();

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
                if (_clock.ElapsedTime >= _beatEndsAt)
                    BeginEncounter();
                return;
            }

            RunSpawnSchedule(dt);

            for (var i = 0; i < _enemies.Count; i++)
                _enemies[i].Regenerate(dt);

            ResolveStrike(dt);
            ResolveCast(dt);
            ResolveEnemyStrikes(dt);

            if (_hero.IsDown)
            {
                End();
                HeroDowned?.Invoke();
                return;
            }

            // Checked after the death test, so a tick that both drops the hero below the auto-Recall
            // fraction and kills it is a Death, not a Recall — the penalty is not dodgeable by
            // the trigger racing it.
            if (WantsToRecall())
            {
                End();
                RecallRequested?.Invoke();
                return;
            }

            if (RosterSpent && _enemies.Count == 0)
                ClearEncounter();
        }

        /// <summary>
        /// Either of <see cref="HeroBehaviour"/>'s two auto-Recall triggers (issue #23), read
        /// against this tick's health and bag fill. With no <see cref="IBagGauge"/> wired the
        /// bag-full trigger cannot fire — there is nothing to measure.
        /// </summary>
        private bool WantsToRecall() =>
            _behaviour.ShouldRecallForHealth(_hero.HealthFraction)
            || (_bag != null && _behaviour.ShouldRecallForBagFull(_bag.FillFraction));

        // ─── spawning ────────────────────────────────────────────────────────

        private bool RosterSpent => _spawnedBrute >= _rosterBrute && _spawnedSkirmisher >= _rosterSkirmisher;

        private int RosterRemaining(EnemyArchetype archetype) => RosterOf(archetype) - SpawnedOf(archetype);

        private void BeginEncounter()
        {
            CurrentEncounter++;
            _enemies.Clear();
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
            _enemies.Add(enemy);

            if (archetype == EnemyArchetype.Brute) _spawnedBrute++;
            else _spawnedSkirmisher++;

            EnemySpawned?.Invoke(enemy);
        }

        // ─── the hero's two attacks ──────────────────────────────────────────

        private void ResolveStrike(float dt)
        {
            _strikeTimer += dt;
            var interval = 1f / _hero.AttackSpeed;
            if (_strikeTimer < interval || _enemies.Count == 0) return;

            _strikeTimer = Math.Min(_strikeTimer - interval, interval); // at most one Strike per tick

            var target = LowestHealth();
            if (target == null) return;

            target.ReceivePhysical(_hero.PhysicalDamage);
            HeroStriked?.Invoke();
            if (target.IsDown) Defeat(target);
        }

        private void ResolveCast(float dt)
        {
            _castTimer += dt;
            if (_castTimer < _tuning.CastCadence) return;

            _castTimer = Math.Min(_castTimer - _tuning.CastCadence, _tuning.CastCadence);

            // The CastThreshold latch is stepped every Cast opportunity, before the affordability
            // and target tests short-circuit — it is a hysteresis on the pool, so it has to see
            // the pool on every beat, not only on the beats a Cast could actually land.
            var casting = _behaviour.ShouldCast(_hero.ResourceFraction);

            if (!casting || _hero.Resource < _hero.CastCost || _enemies.Count == 0) return;

            _hero.SpendResource(_hero.CastCost);
            HeroCast?.Invoke();

            var targets = HighestHealth(_tuning.CastTargets);
            for (var i = 0; i < targets.Count; i++)
            {
                targets[i].ReceiveMagical(_hero.MagicalDamage);
                if (targets[i].IsDown) Defeat(targets[i]);
            }
        }

        private void ResolveEnemyStrikes(float dt)
        {
            for (var i = 0; i < _enemies.Count; i++)
            {
                var enemy = _enemies[i];
                var interval = 1f / enemy.AttackSpeed;
                enemy.StrikeTimer += dt;
                if (enemy.StrikeTimer < interval) continue;

                enemy.StrikeTimer = Math.Min(enemy.StrikeTimer - interval, interval);
                _hero.ReceivePhysical(enemy.StrikeDamage);
            }
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

        // ─── deterministic targeting ─────────────────────────────────────────

        private Enemy LowestHealth()
        {
            Enemy best = null;
            for (var i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e.IsDown) continue;
                if (best == null || e.Health < best.Health)
                    best = e;
            }
            return best;
        }

        private List<Enemy> HighestHealth(int count)
        {
            _castTargets.Clear();
            for (var i = 0; i < _enemies.Count; i++)
                if (!_enemies[i].IsDown)
                    _castTargets.Add(_enemies[i]);

            _castTargets.Sort(CompareForCast);

            if (_castTargets.Count > count)
                _castTargets.RemoveRange(count, _castTargets.Count - count);
            return _castTargets;
        }

        // Highest health first; spawn order breaks ties so the choice is total and deterministic.
        private static int CompareForCast(Enemy a, Enemy b)
        {
            var byHealth = b.Health.CompareTo(a.Health);
            return byHealth != 0 ? byHealth : a.SpawnIndex.CompareTo(b.SpawnIndex);
        }
    }
}
