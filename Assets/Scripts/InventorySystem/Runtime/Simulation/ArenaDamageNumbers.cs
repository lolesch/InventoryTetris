using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The damage number feedback (issues #181, #213): each hit that lands - the hero's Strike and Cast on an enemy,
    /// an enemy's Strike on the hero - raises a number that rises and fades. It listens to the Encounter's
    /// <see cref="EncounterSimulation.HitLanded"/>, which carries the target, the damage type and the amounts the
    /// health event never knew; the hit flash and the hit shake keep listening to the enemy's health, since they only
    /// need to know a hit landed. <see cref="EnemyArena"/> binds it behind its <c>damageNumbers</c> switch.
    /// <para>
    /// A coarse frame runs several ticks and each can land a hit, so they are summed in a
    /// <see cref="DamageAccumulator{TKey}"/> keyed by target <i>and</i> damage type and flushed once per frame: a
    /// Strike and a Cast on one enemy give two numbers, and so do two enemies. The number shows the amount lost
    /// (a hit the target mitigates fully shows nothing); its size and tint come from <see cref="DamageNumberStyle"/>,
    /// the raw amount of <i>one</i> hit (the frame's mean, so a coarse frame does not saturate it) read against
    /// <see cref="DamageReference"/> - the best the dealer could do with that type.
    /// </para>
    /// <para>
    /// The killing blow: <see cref="EncounterSimulation.HitLanded"/> raises before the enemy is removed, and a
    /// number's origin is the sim's own <see cref="Enemy.Position"/> projected by the arena, so a fallen enemy still
    /// has one when the flush comes. A number with nowhere to rise from (no anchor) is dropped.
    /// </para>
    /// <para>
    /// Subscribed in <see cref="Bind"/> and released in <see cref="Unbind"/>, driven from the arena's bind path and
    /// not from <c>Awake</c>/<c>OnDisable</c>: the scene object survives a Play session under disabled domain reload
    /// (<c>codebase-notes.md</c>). Pending damage is dropped on <see cref="Unbind"/>: the Encounter is gone.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaDamageNumbers : MonoBehaviour
    {
        [SerializeField, Tooltip("The figure that rises. It is pooled and parented in the arena, beside the views.")]
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
        private EncounterSimulation _encounter;
        private RunState _run;
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
            _run = SimulationService.Instance.Run;
            _encounter = encounter;
            _encounter.HitLanded += OnHitLanded;
        }

        /// <summary>Stop listening and forget everything queued: the Encounter is over. Safe when unbound.</summary>
        public void Unbind()
        {
            if (_encounter != null)
                _encounter.HitLanded -= OnHitLanded;

            _hits.Flush(static (_, _, _, _) => { });
            _hasPending = false;
            _origins = null;
            _encounter = null;
            _run = null;
        }

        private void OnHitLanded(HitEvent hit)
        {
            // A hit the target mitigates fully lands but loses nothing: there is no figure to show.
            if (hit.LostAmount <= 0f)
                return;

            _hits.Add((hit.Target, hit.DamageType), hit.LostAmount, hit.RawAmount);
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
        }

        private void ShowNumber((ICombatant Target, DamageType Type) key, float lost, float rawSum, int hits)
        {
            // The Run ended (Recall, hero death, Relocate) or the arena is going down: nothing is shown.
            if (!isActiveAndEnabled || _encounter == null || _run == null || _run.Encounter != _encounter)
                return;

            if (!_origins.TryGetNumberOrigin(key.Target, out var origin))
                return;

            // The size reads one hit (the frame's mean), not the sum: the reference is the best single hit, so at
            // high sim speed several ordinary hits summed would saturate it. The label still shows the summed loss.
            var raw = rawSum / Mathf.Max(1, hits);
            var style = DamageNumberStyle.Of(raw, ReferenceFor(key.Target, key.Type), key.Type,
                minFontSize, maxFontSize, physicalTint, magicalTint);

            var number = Pool.GetObject();
            number.Show(origin + startOffset, lost, style, lifetime, riseHeight, holdFraction, _encounter, Pool.ReleaseObject);
        }

        // The best raw hit of the type the dealer can land: the Location's strongest for a hit on the hero,
        // the hero's own stat for a hit on an enemy.
        private float ReferenceFor(ICombatant target, DamageType type) =>
            ReferenceEquals(target, _encounter.Hero)
                ? DamageReference.OfEnemies(_encounter.Profile, type, _encounter.DamageSpread)
                : DamageReference.OfHero(_encounter.Hero, type, _encounter.DamageSpread);
    }
}
