using System.Collections.Generic;
using Submodules.Utility.Extensions;
using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The damage number feedback (issues #181, #213): each hit the Encounter reports raises a number that rises and
    /// fades. A frame's hits are summed per target and damage type, since a coarse frame runs several ticks, and a number
    /// rises from the place the hit carries so a killing blow needs no figure left to read.
    /// <para>
    /// Driven from the arena's bind path, not <c>Awake</c>/<c>OnDisable</c>: the scene object survives a Play session
    /// under disabled domain reload (<c>codebase-notes.md</c>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaDamageNumbers : MonoBehaviour
    {
        [SerializeField, Tooltip("The figure that rises. It is pooled and parented in the arena, beside the figures.")]
        private DamageNumber prefab;
        [SerializeField, Min(1f), Tooltip("Font size of a hit that does nothing (the bottom of the size range).")]
        private float minFontSize = 22f;
        [SerializeField, Min(1f), Tooltip("Font size of a hit as big as the dealer's best of that damage type (the top of the size range).")]
        private float maxFontSize = 44f;
        [SerializeField, Tooltip("Physical damage - the colour the numbers always had.")]
        private Color physicalTint = new(1f, 0.82f, 0.25f, 1f);
        [SerializeField, Tooltip("Magical damage: a dark blue-purple.")]
        private Color magicalTint = new(0.36f, 0.24f, 0.85f, 1f);
        [SerializeField, Tooltip("Canvas units from the figure's centre where a number starts.")]
        private Vector2 startOffset = new(0f, 40f);
        [SerializeField, Min(0f), Tooltip("Canvas units a number rises.")]
        private float riseHeight = 60f;
        [SerializeField, Min(0.01f), Tooltip("Sim seconds a number lives. Scales with sim speed and freezes on pause.")]
        private float lifetime = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the life it stays opaque before fading out.")]
        private float holdFraction = 0.4f;

        private readonly DamageAccumulator<(ICombatant Target, DamageType Type)> _hits = new();
        private PrefabPool<DamageNumber> _pool;
        private RectTransform _space;
        private IDamageNumberOrigins _origins;
        // What the frame's last hit on a key said: the scale its size is judged against and where its number starts.
        private readonly Dictionary<(ICombatant Target, DamageType Type), (float Reference, Coordinate Place)> _stamps = new();
        private readonly List<DamageNumber> _live = new();
        private EncounterSimulation _encounter;
        private bool _hasPending;

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload. Rebuilt when the space it lives in changes.
        private PrefabPool<DamageNumber> Pool => _pool ??= new PrefabPool<DamageNumber>(prefab, _space);

        /// <summary>Whether this component is listening to an Encounter's hits.</summary>
        public bool IsBound => _encounter != null;

        /// <summary>Whether hits are waiting for this frame's flush.</summary>
        public bool HasPending => _hasPending;

        /// <summary>
        /// Show a number for each frame's worth of hits <paramref name="encounter"/> lands, in
        /// <paramref name="space"/>, rising from where <paramref name="origins"/> says the target is.
        /// </summary>
        public void Bind(EncounterSimulation encounter, IDamageNumberOrigins origins, RectTransform space)
        {
            Unbind();

            if (prefab == null)
                return;

            if (_space != space)
                _pool = null;

            _space = space;
            _origins = origins;
            _encounter = encounter;
            _encounter.HitLanded += OnHitLanded;
        }

        /// <summary>
        /// Stop listening, forget everything queued and call back every rising number: the Encounter is over, so
        /// nothing outlives it. Safe when unbound.
        /// </summary>
        public void Unbind()
        {
            if (_encounter != null)
                _encounter.HitLanded -= OnHitLanded;

            _hits.Flush(static (_, _, _, _) => { });
            _stamps.Clear();
            _hasPending = false;

            for (var i = _live.Count; i-- > 0;)
                _live[i].Dismiss();

            _origins = null;
            _encounter = null;
        }

        private void OnHitLanded(HitEvent hit)
        {
            // A hit the target mitigates fully lands but loses nothing: there is no figure to show.
            if (hit.LostAmount <= 0f)
                return;

            var key = (hit.Target, hit.DamageType);
            _hits.Add(key, hit.LostAmount, hit.RawAmount);
            _stamps[key] = (hit.Reference, hit.TargetPosition);
            _hasPending = true;
        }

        private void Update() => Flush();

        /// <summary>One number per target and type for everything landed since the last flush.</summary>
        private void Flush()
        {
            if (!_hasPending)
                return;

            _hasPending = false;
            _hits.Flush(ShowNumber);
            _stamps.Clear();
        }

        private void ShowNumber((ICombatant Target, DamageType Type) key, float lost, float rawSum, int hits)
        {
            // Unbound (the Run ended: Return, hero death, Relocate) or the arena is going down: nothing is shown.
            if (!isActiveAndEnabled || _encounter == null)
                return;

            var (reference, place) = _stamps[key];
            if (!_origins.TryGetNumberOrigin(place, out var origin))
                return;

            // The size reads one hit (the frame's mean), not the sum: the reference is the best single hit, so at
            // high sim speed several ordinary hits summed would saturate it. The label still shows the summed loss.
            var raw = rawSum / Mathf.Max(1, hits);
            var style = DamageNumberStyle.Of(raw, reference, key.Type,
                minFontSize, maxFontSize, physicalTint, magicalTint);

            var number = Pool.GetObject();
            _live.Add(number);
            number.Show(origin + startOffset, lost, style, lifetime, riseHeight, holdFraction, Release);
        }

        private void Release(DamageNumber number)
        {
            _ = _live.Remove(number);
            Pool.ReleaseObject(number);
        }
    }
}
