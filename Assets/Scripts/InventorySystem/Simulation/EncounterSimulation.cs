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
    /// An Encounter clears when its Roster is spent and the last body falls: XP settles then,
    /// summed over the Roster; a short beat; the next Encounter. The sim runs Encounters
    /// endlessly — only the hero going down (or an external <see cref="Abandon"/> on Recall /
    /// Death, issue #21) ends it. Loot and coins are a per-kill concern layered on
    /// <see cref="EnemyDefeated"/> by issue #24; the <c>CastThreshold</c> hysteresis and the
    /// retreat / bag triggers are issue #23.
    ///
    /// Engine-free and deterministic: every roll (Roster size, spawn type, Pack size, jitter,
    /// spawn desync) is drawn from the injected <see cref="IRollSource"/>, in that order.
    /// It never references <c>BaseCharacter</c>.
    /// </summary>
    public sealed class EncounterSimulation
    {
        private readonly IHeroCombatant _hero;
        private readonly EncounterProfile _profile;
        private readonly IRollSource _rolls;
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
        private float _endedAt;

        private float _pot;

        public EncounterSimulation(
            IHeroCombatant hero,
            EncounterProfile profile,
            IRollSource rolls,
            int engagementTarget,
            EncounterTuning tuning = null)
        {
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rolls = rolls ?? throw new ArgumentNullException(nameof(rolls));
            if (engagementTarget < 1)
                throw new ArgumentOutOfRangeException(nameof(engagementTarget), engagementTarget, "Engagement target must be at least 1.");

            _tuning = tuning ?? new EncounterTuning();
            _tuning.Validate();
            EngagementTarget = engagementTarget;

            _clock = new CombatClock(_tuning.Tick, _tuning.MaxTicksPerAdvance);
            _clock.OnTick += Step;

            BeginEncounter();
        }

        /// <summary>
        /// The soft count of enemies the fight refills toward (the Engagement slider — issue
        /// #23 writes it on the slider's change event; the sim reads it every spawn tick). Not
        /// a ceiling: a Pack that arrives while the count is below the target overshoots it.
        /// </summary>
        public int EngagementTarget { get; set; }

        /// <summary>The Location this Encounter is fought at — its loot table and source level (issue #24).</summary>
        public EncounterProfile Profile => _profile;

        /// <summary>The hero fighting this Encounter — its live magic find and item quantity (issue #24).</summary>
        public IHeroCombatant Hero => _hero;

        public SimulationPhase Phase { get; private set; } = SimulationPhase.Fighting;

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

        /// <summary>The in-progress Encounter's XP pot — settled on the clear, forfeited on an exit.</summary>
        public float UnsettledXp => _pot;

        /// <summary>Sum of every cleared Encounter's XP settlement.</summary>
        public int SettledXp { get; private set; }

        /// <summary>XP lost because the fight ended (hero down or <see cref="Abandon"/>) before a clear.</summary>
        public int ForfeitedXp { get; private set; }

        /// <summary>Raised as each body falls — the per-kill seam issue #24 rolls loot and coins on.</summary>
        public event Action<Enemy> EnemyDefeated;

        /// <summary>Raised on an Encounter clear, carrying the XP just settled.</summary>
        public event Action<int> EncounterCleared;

        /// <summary>Raised the tick the hero's health reaches 0.</summary>
        public event Action HeroDowned;

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
        /// End the fight without a clear — the Run was Recalled or the hero Died (issue #21).
        /// Forfeits the in-progress Encounter's pot and returns the amount lost.
        /// </summary>
        public int Abandon()
        {
            if (Phase == SimulationPhase.Ended) return 0;
            var forfeited = ForfeitPot();
            End();
            return forfeited;
        }

        private int ForfeitPot()
        {
            var forfeited = (int)Math.Round(_pot, MidpointRounding.AwayFromZero);
            ForfeitedXp += forfeited;
            _pot = 0f;
            return forfeited;
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

            if (Phase == SimulationPhase.Beat)
            {
                _hero.Regenerate(dt);
                if (_clock.ElapsedTime >= _beatEndsAt)
                    BeginEncounter();
                return;
            }

            RunSpawnSchedule(dt);

            _hero.Regenerate(dt);
            for (var i = 0; i < _enemies.Count; i++)
                _enemies[i].Regenerate(dt);

            ResolveStrike(dt);
            ResolveCast(dt);
            ResolveEnemyStrikes(dt);

            if (_hero.IsDown)
            {
                ForfeitPot();
                End();
                HeroDowned?.Invoke();
                return;
            }

            if (RosterSpent && _enemies.Count == 0)
                ClearEncounter();
        }

        // ─── spawning ────────────────────────────────────────────────────────

        private bool RosterSpent => _spawnedBrute >= _rosterBrute && _spawnedSkirmisher >= _rosterSkirmisher;

        private int RosterRemaining(EnemyArchetype archetype) => RosterOf(archetype) - SpawnedOf(archetype);

        private void BeginEncounter()
        {
            CurrentEncounter++;
            _enemies.Clear();
            _spawnedBrute = 0;
            _spawnedSkirmisher = 0;
            _pot = 0f;
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

            _spawnTimer = _profile.SpawnInterval + ((float)_rolls.Next() * 2f - 1f) * _profile.SpawnJitter;
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
            if (target.IsDown) Defeat(target);
        }

        private void ResolveCast(float dt)
        {
            _castTimer += dt;
            if (_castTimer < _tuning.CastCadence) return;

            _castTimer = Math.Min(_castTimer - _tuning.CastCadence, _tuning.CastCadence);

            // Issue #20 fires whenever a Cast is affordable; issue #23 layers the CastThreshold
            // hysteresis (hold below a Resource fraction, then burn to empty) over this gate.
            if (_hero.Resource < _hero.CastCost || _enemies.Count == 0) return;

            _hero.SpendResource(_hero.CastCost);

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
            _pot += Math.Max(0f, balanced);

            EnemyDefeated?.Invoke(enemy);
        }

        private void ClearEncounter()
        {
            EncountersCleared++;

            var settled = (int)Math.Round(_pot, MidpointRounding.AwayFromZero);
            SettledXp += settled;
            _pot = 0f;

            Phase = SimulationPhase.Beat;
            _beatEndsAt = _clock.ElapsedTime + _tuning.Beat;

            EncounterCleared?.Invoke(settled);
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
