using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// A Town Portal's marker on one map (ADR-0022). It is shut until it is told to grow: it waits a beat, then scales up,
    /// its width from 0 and its height from half, and only then <see cref="IsOpen"/> says the hero may go through it or
    /// come out of it. A portal that was open before the player looked (a loaded hero's) is opened at once.
    /// </summary>
    public abstract class PortalMarker : AbstractButton
    {
        [SerializeField, Min(0f), Tooltip("Seconds the portal takes to grow from a sliver to its full size.")]
        private float appearSeconds = 0.4f;
        [SerializeField, Min(0f), Tooltip("Seconds the portal waits before it grows when the hero comes out of it: the map is still fading in.")]
        private float arrivalDelaySeconds = 0.3f;

        private Vector3? _baseScale;
        private bool _growing;
        private float _elapsed;
        private float _delay;

        /// <summary>Whether it has grown to its full size and is still open: the hero goes through it or comes out of it now.</summary>
        public bool IsOpen => _growing && PortalAppear.Progress(_elapsed, _delay, appearSeconds) >= 1f;

        /// <summary>Seconds to wait before growing when the hero comes out of it.</summary>
        protected float ArrivalDelay => arrivalDelaySeconds;

        /// <summary>Starts from shut: waits <paramref name="delaySeconds"/>, then grows.</summary>
        protected void Grow(float delaySeconds)
        {
            _growing = true;
            _delay = delaySeconds;
            _elapsed = 0f;
            ApplyScale();
        }

        /// <summary>Open and whole, with no growing to watch.</summary>
        protected void OpenAtOnce()
        {
            _growing = true;
            _delay = 0f;
            _elapsed = appearSeconds;
        }

        /// <summary>Shut: not open any more, whatever it had grown to.</summary>
        protected void Close() => _growing = false;

        /// <summary>Advances the growing by this frame and sets the scale; once a frame while the portal is shown.</summary>
        protected void Tick()
        {
            if (_growing)
                _elapsed += Time.deltaTime;

            ApplyScale();
        }

        private void ApplyScale()
        {
            _baseScale ??= transform.localScale;
            var (x, y) = PortalAppear.Scale(_growing ? PortalAppear.Progress(_elapsed, _delay, appearSeconds) : 0f);
            var whole = _baseScale.Value;
            transform.localScale = new Vector3(whole.x * x, whole.y * y, whole.z);
        }
    }
}
