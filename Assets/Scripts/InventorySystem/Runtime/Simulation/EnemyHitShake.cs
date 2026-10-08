using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The hit shake feedback (issue #180): the enemy's sprite jolts each time its health drops and settles
    /// over a short stretch of <i>sim</i> time. It sits on the <c>Sprite</c> child of the view and moves
    /// that child's own anchored position by a local offset, never the view's root: the health bar stays
    /// still, the arena's placement (<see cref="EnemyView.Place"/>) keeps owning the root, and the
    /// facing flip (a <c>localScale</c> on the same child) does not touch an anchored position, so the
    /// two compose without fighting.
    /// <para>
    /// It listens to the enemy's own <see cref="CharacterResource.CurrentHasChanged"/>, so the sim is
    /// untouched, and nothing else of the view knows about it: <see cref="EnemyArena"/> binds it behind its
    /// <c>hitShake</c> switch and <see cref="EnemyView.Unbind"/> lets it go. It shares the event with
    /// <see cref="EnemyHitFlash"/>, not a component, so either prunes alone. Pruning the feature is
    /// deleting this component and those call sites.
    /// </para>
    /// <para>
    /// Subscribed in <see cref="Bind"/> and released in <see cref="Unbind"/>, driven from the arena's
    /// bind path and not from <c>Awake</c>/<c>OnDisable</c>: the scene object survives a Play session
    /// under disabled domain reload (<c>codebase-notes.md</c>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class EnemyHitShake : MonoBehaviour
    {
        [SerializeField, Min(0f), Tooltip("Canvas units of the first kick.")]
        private float amplitude = 6f;
        [SerializeField, Min(0f), Tooltip("Wobbles per sim second.")]
        private float frequency = 20f;
        [SerializeField, Min(0.01f), Tooltip("Sim seconds a shake takes to settle. Scales with sim speed and freezes on pause.")]
        private float duration = 0.2f;

        private readonly HitShake _shake = new();
        private CharacterResource _health;
        private RectTransform _rect;
        private Vector2 _rest;
        private bool _hasRest;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        /// <summary>The shake offset now, zero when settled.</summary>
        public Vector2 Offset => _shake.Offset(amplitude, frequency);

        /// <summary>Whether a shake is in flight.</summary>
        public bool IsShaking => _shake.IsActive;

        /// <summary>Whether this component is listening to an enemy's health.</summary>
        public bool IsBound => _health != null;

        /// <summary>Shake each time <paramref name="enemy"/>'s health drops.</summary>
        public void Bind(Enemy enemy)
        {
            Unbind();

            _health = enemy.HealthResource;
            _health.CurrentHasChanged += OnHealthChanged;
        }

        /// <summary>
        /// Stop listening but let the shake in flight settle: a dying view takes no more hits, yet the
        /// killing blow's shake (raised before the defeat) should still play. Safe when unbound.
        /// </summary>
        public void Detach()
        {
            if (_health == null)
                return;

            _health.CurrentHasChanged -= OnHealthChanged;
            _health = null;
        }

        /// <summary>Stop listening and put the sprite back at rest: a pooled view starts with zero offset. Safe when unbound.</summary>
        public void Unbind()
        {
            Detach();

            _shake.Reset();
            Apply();
        }

        private void OnHealthChanged(float previous, float current, float total)
        {
            if (HitShake.IsHit(previous, current))
                _shake.Hit();
        }

        private void Update()
        {
            if (!_shake.IsActive)
                return;

            _shake.Advance(SimulationService.Instance.SimDelta(Time.deltaTime), duration);
            Apply();
        }

        // The rest position is the prefab's own, read before the first offset is ever written.
        private void Apply()
        {
            if (!_hasRest)
            {
                _rest = Rect.anchoredPosition;
                _hasRest = true;
            }

            Rect.anchoredPosition = _rest + Offset;
        }
    }
}
