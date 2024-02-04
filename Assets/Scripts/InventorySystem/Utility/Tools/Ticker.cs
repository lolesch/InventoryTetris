using System;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Utility
{
    [Serializable]
    /// <summary> A Ticker adds up it's progress to reach a predefined duration by a given tickDelta </summary>
    public class Ticker
    {
        public Ticker(float duration, bool isDelayed = false)
        {
            this.duration = duration;
            progress = isDelayed ? duration : 0f;
        }

        public readonly float duration;

        [SerializeField] protected float progress;

        /// <summary> progress has not reached duration </summary>
        public bool HasRemainingDuration => progress < duration;

        public float Progress01 => HasRemainingDuration ? Mathf.Clamp01(progress / duration) : 1f;

        //public float Remaining => duration - progress;

        /// <summary> Increases the timers progress by tickInterval </summary>
        public void Tick(float tickInverval) => progress += tickInverval;

        /// <summary> Sets the current progress to 0 </summary>
        public void Start() => progress = 0f;

        /// <summary> Subtracts the duration from the current progress</summary>
        public void Restart() => progress -= duration;
    }
}
