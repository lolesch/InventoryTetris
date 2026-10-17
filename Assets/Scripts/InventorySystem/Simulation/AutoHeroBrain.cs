using System;
using System.Collections.Generic;
using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The default hero Brain: the player's Behaviour Profile carried out without the player at the controls.
    /// It asks freely (a Strike before arrival, a Cast with nothing to catch); the Encounter refuses what the rules forbid.
    /// </summary>
    internal sealed class AutoHeroBrain : IHeroBrain
    {
        private readonly HeroBehaviour _behaviour;
        private readonly HeroTargeting _targeting;
        private Enemy _target; // the sticky target the last tick chose; may have fallen since

        public AutoHeroBrain(HeroBehaviour behaviour, HeroTargeting targeting)
        {
            _behaviour = behaviour ?? throw new ArgumentNullException(nameof(behaviour));
            _targeting = targeting;
        }

        public void Decide(in HeroView view, List<Intent> intents)
        {
            _target = Choose(view);
            if (_target == null)
                intents.Add(Intent.Walk(view.Origin));
            else
            {
                intents.Add(Intent.Approach(_target));
                if (view.StrikeReady)
                    intents.Add(Intent.Strike(_target));
            }

            // Stepped on every open beat, before anything else can short-circuit: the latch arms on the pool
            // charging up, however idle the beat.
            if (view.CastBeatOpen && _behaviour.ShouldCast(view.ResourceFraction))
                intents.Add(Intent.Cast());
        }

        public Enemy PeekTarget(in HeroView view) => Choose(view);

        private Enemy Choose(in HeroView view) => _targeting switch
        {
            HeroTargeting.WeightedProximity => WeightedProximityTarget(view),
            _ => throw new InvalidOperationException($"Unknown hero targeting {_targeting}."),
        };

        /// <summary>
        /// Keep the target until it falls. With none, the best-scoring living enemy anywhere. A target out of
        /// Strike Range gives way to the best-scoring enemy that is inside it, if any is; otherwise he keeps it
        /// and walks.
        /// </summary>
        private Enemy WeightedProximityTarget(in HeroView view)
        {
            var current = _target != null && !_target.IsDown ? _target : null;
            if (current == null)
                return BestScoring(view, inReachOnly: false);

            if (view.InRange(current.Position, view.StrikeRange))
                return current;

            return BestScoring(view, inReachOnly: true) ?? current;
        }

        /// <summary>
        /// The living enemy with the lowest <c>w * distance from the origin + (1 - w) * distance from the hero</c>,
        /// <c>w</c> being <see cref="HeroBehaviour.OriginWeight"/> read live; the earliest spawned on a tie.
        /// </summary>
        private Enemy BestScoring(in HeroView view, bool inReachOnly)
        {
            var weight = Math.Clamp(_behaviour.OriginWeight, 0f, 1f);

            Enemy best = null;
            var bestScore = 0f;
            for (var i = 0; i < view.Enemies.Count; i++)
            {
                var e = view.Enemies[i];
                if (e.IsDown || (inReachOnly && !view.InRange(e.Position, view.StrikeRange))) continue;

                var score = weight * Coordinate.Distance(e.Position, view.Origin)
                    + (1f - weight) * Coordinate.Distance(e.Position, view.Position);
                if (best == null || score < bestScore) // strict: the earlier spawn keeps a tie
                {
                    best = e;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
