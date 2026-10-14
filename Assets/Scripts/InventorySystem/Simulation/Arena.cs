using System;
using System.Collections.Generic;
using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The spatial module of an Encounter (ADR-0020): reach, walks, Cast coverage and spawn placement. It
    /// answers and moves; it never decides, deals damage or rolls. Enemy positions live on the enemy, the hero's here.
    /// </summary>
    internal sealed class Arena
    {
        // Slack on every range test: an enemy that stops exactly at its range lands on it within float error.
        private const float RangeSlack = 0.001f;

        private readonly ArenaTuning _tuning;
        private readonly List<float> _bearings = new(); // reused buffer - a spawn sorts the living bearings
        private readonly List<Enemy> _covered = new(); // reused buffer - the Cast runs on a cadence

        public Arena(ArenaTuning tuning)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            HeroPosition = tuning.Origin;
        }

        public ArenaTuning Tuning => _tuning;

        public Coordinate Origin => _tuning.Origin;

        /// <summary>Where the hero stands. He starts at the origin and moves only by <see cref="WalkHero"/>.</summary>
        public Coordinate HeroPosition { get; private set; }

        // ─── reach ───────────────────────────────────────────────────────────

        /// <summary>Whether <paramref name="other"/> stands within <paramref name="range"/> of the hero (a Strike's or the Cast's).</summary>
        public bool InRangeOfHero(Coordinate other, float range) =>
            Coordinate.Distance(HeroPosition, other) <= range + RangeSlack;

        // ─── walking ─────────────────────────────────────────────────────────

        /// <summary>
        /// Step the hero toward <paramref name="toward"/> for one tick, stopping <paramref name="stopDistance"/> short
        /// of it and never past. <paramref name="movementStat"/> is his <c>MovementSpeed</c> stat, converted to arena
        /// units by the tuning's scale. A hero with no walking speed stands where he is.
        /// </summary>
        public void WalkHero(float movementStat, float deltaSeconds, Coordinate toward, float stopDistance) =>
            HeroPosition = Approach(HeroPosition, toward, WalkingSpeed(movementStat) * _tuning.MovementSpeedScale * deltaSeconds, stopDistance);

        /// <summary>Step <paramref name="enemy"/> toward the hero for one tick, stopping at its own stop distance.</summary>
        public void WalkEnemy(Enemy enemy, float deltaSeconds) =>
            enemy.Position = Approach(enemy.Position, HeroPosition, WalkingSpeed(enemy.MovementSpeed) * deltaSeconds, enemy.StopDistance);

        private static Coordinate Approach(Coordinate from, Coordinate to, float step, float stopDistance)
        {
            var excess = Coordinate.Distance(from, to) - stopDistance;
            if (excess <= 0f || step <= 0f) return from;

            return Coordinate.MoveTowards(from, to, Math.Min(step, excess));
        }

        /// <summary>
        /// A movement speed the sim can walk at: a stat modified below zero stands still instead of walking
        /// backwards, and a NaN one (which every comparison refuses) stands still instead of poisoning a position.
        /// </summary>
        private static float WalkingSpeed(float speed) => speed > 0f ? speed : 0f;

        // ─── spawning ────────────────────────────────────────────────────────

        /// <summary>
        /// Stand a fresh enemy on the spawn ring at a bearing chosen among the <paramref name="living"/> and fix how
        /// close it will walk in. The caller rolls: <paramref name="bearingRoll"/> then <paramref name="stopRoll"/>,
        /// both 0..1.
        /// </summary>
        public void PlaceSpawn(Enemy enemy, IReadOnlyList<Enemy> living, float bearingRoll, float stopRoll)
        {
            var bearing = SpawnBearing(living, bearingRoll);

            enemy.Bearing = bearing;
            enemy.Position = _tuning.Origin
                + Coordinate.Rotate(new Coordinate(1f, 0f), bearing) * (_tuning.Radius + _tuning.SpawnMargin);
            enemy.StopDistance = enemy.StrikeRange * (1f - _tuning.StopJitter * stopRoll);
        }

        /// <summary>
        /// The middle of the widest gap between the living enemies' bearings, strayed by the jitter; the first
        /// such gap on a tie, and the whole circle when nobody lives. A Pack spreads out because each arrival
        /// bisects what the last one left.
        /// </summary>
        public float SpawnBearing(IReadOnlyList<Enemy> living, float roll)
        {
            _bearings.Clear();
            for (var i = 0; i < living.Count; i++)
                if (!living[i].IsDown)
                    _bearings.Add(living[i].Bearing);
            _bearings.Sort();

            var start = 0f;
            var width = 360f;
            for (var i = 0; i < _bearings.Count; i++)
            {
                var end = i + 1 < _bearings.Count ? _bearings[i + 1] : _bearings[0] + 360f;
                if (i == 0 || end - _bearings[i] > width)
                {
                    start = _bearings[i];
                    width = end - _bearings[i];
                }
            }

            return (start + width * (0.5f + (roll - 0.5f) * _tuning.BearingJitter)) % 360f;
        }

        // ─── shapes ──────────────────────────────────────────────────────────

        /// <summary>
        /// The enemy a Cast is aimed at: of those within the Cast's range of the hero, the one whose shape (placed per
        /// the anchor) holds the most enemies; a tie goes to the one nearest the hero, then the earliest in
        /// <paramref name="enemies"/>. Null when no candidate's shape holds anyone.
        /// </summary>
        public Enemy DensestCluster(IReadOnlyList<Enemy> enemies, CastDefinition cast)
        {
            var shape = cast.Shape.Scaled(cast.Size);

            Enemy best = null;
            var bestCount = 0;
            var bestDistance = 0f;

            for (var i = 0; i < enemies.Count; i++)
            {
                var candidate = enemies[i];
                if (candidate.IsDown || !InRangeOfHero(candidate.Position, cast.Range)) continue;

                var count = 0;
                for (var j = 0; j < enemies.Count; j++)
                    if (Catches(shape, cast.Anchor, candidate, enemies[j]))
                        count++;

                var distance = Coordinate.Distance(HeroPosition, candidate.Position);
                if (count > bestCount || (count == bestCount && count > 0 && distance < bestDistance))
                {
                    best = candidate;
                    bestCount = count;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// The living enemies inside the cast's shape placed for a Cast aimed at <paramref name="aim"/>, in the order
        /// given. The list is reused: it is valid until the next call.
        /// </summary>
        public IReadOnlyList<Enemy> InShape(IReadOnlyList<Enemy> enemies, CastDefinition cast, Enemy aim)
        {
            var shape = cast.Shape.Scaled(cast.Size);

            _covered.Clear();
            for (var i = 0; i < enemies.Count; i++)
                if (Catches(shape, cast.Anchor, aim, enemies[i]))
                    _covered.Add(enemies[i]);
            return _covered;
        }

        private bool Catches(AreaShape shape, AreaAnchor anchor, Enemy aim, Enemy enemy) =>
            !enemy.IsDown && shape.Contains(enemy.Position, anchor, HeroPosition, aim.Position);
    }
}
