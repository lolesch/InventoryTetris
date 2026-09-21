using System;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// Fixed-tick heartbeat for the Encounter simulation (ADR-0008). Advances on a discrete
    /// interval decoupled from frame time: each <see cref="Advance"/> banks real elapsed time
    /// into an accumulator and fires <see cref="OnTick"/> once per whole interval (0..N times
    /// per call), carrying the remainder forward. This makes the simulation frame-rate
    /// independent — the same total elapsed time yields the same number of ticks regardless of
    /// how it is chunked.
    ///
    /// Pure and engine-agnostic so combat can be unit-tested by feeding deltas directly; in
    /// play a <c>MonoBehaviour</c> driver advances it from its <c>Update</c> loop with
    /// <c>clock.Advance(dt * simSpeed)</c>. It deliberately does NOT use the shared
    /// <c>Utility</c> submodule's <c>Timer</c> / <c>TimerTicker</c> — that is one global
    /// player-loop driver that also ticks UI timers, so routing combat through it would make
    /// the sim-speed slider drag every panel tween along with it (ADR-0008).
    ///
    /// Ported from the sister project AutoBattler
    /// (<c>Assets/Code/Runtime/Core/Combat/CombatClock.cs</c>); the one change is
    /// <c>Mathf.Max</c> → <c>System.Math.Max</c> so the type names no <c>UnityEngine</c> type.
    /// </summary>
    public sealed class CombatClock
    {
        private readonly float _tickInterval;
        private readonly int   _maxTicksPerAdvance;
        private float          _accumulator;
        private float          _elapsed;

        /// <summary>Raised once per elapsed tick interval.</summary>
        public event Action OnTick;

        /// <param name="tickInterval">Seconds per tick (e.g. 0.05 for 20 ticks/sec).</param>
        /// <param name="maxTicksPerAdvance">
        /// Upper bound on ticks fired in a single <see cref="Advance"/> call, guarding against
        /// the "spiral of death" after a long frame hitch. Surplus accumulated time is dropped.
        /// </param>
        public CombatClock(float tickInterval, int maxTicksPerAdvance = 8)
        {
            if (tickInterval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tickInterval), tickInterval, "Tick interval must be positive.");

            _tickInterval       = tickInterval;
            _maxTicksPerAdvance = Math.Max(1, maxTicksPerAdvance);
        }

        public float TickInterval => _tickInterval;

        /// <summary>
        /// Simulated time since the last <see cref="Reset"/>: ticks fired × interval, not wall clock.
        /// Quantised to the tick on purpose — the tick is the only time the simulation can observe, so
        /// a timestamp taken from here always names a moment the simulation actually had.
        /// Surplus dropped by the spiral-of-death clamp doesn't advance it either, so a frame hitch
        /// slows the clock rather than teleporting it past events that never resolved.
        /// </summary>
        public float ElapsedTime => _elapsed;

        /// <summary>
        /// Banks <paramref name="deltaTime"/> and fires <see cref="OnTick"/> for every whole
        /// interval now elapsed. Returns the number of ticks fired.
        /// </summary>
        public int Advance(float deltaTime)
        {
            if (deltaTime <= 0f) return 0;

            _accumulator += deltaTime;

            var fired = 0;
            while (_accumulator >= _tickInterval && fired < _maxTicksPerAdvance)
            {
                _accumulator -= _tickInterval;
                _elapsed     += _tickInterval;
                fired++;
                // Advance the clock before the tick runs, so anything that resolves inside this tick
                // (an attack, a defeat) timestamps at the tick it belongs to rather than the previous one.
                OnTick?.Invoke();
            }

            // Drop the backlog beyond the clamp so a hitch can't keep firing on later frames.
            if (_accumulator >= _tickInterval)
                _accumulator = 0f;

            return fired;
        }

        /// <summary>
        /// Clears the accumulated remainder and zeroes <see cref="ElapsedTime"/>. Use when (re)starting
        /// combat — each fight's timeline starts at 0:00.
        /// </summary>
        public void Reset()
        {
            _accumulator = 0f;
            _elapsed     = 0f;
        }
    }
}
