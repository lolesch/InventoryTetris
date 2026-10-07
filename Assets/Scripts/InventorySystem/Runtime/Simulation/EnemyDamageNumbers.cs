using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The damage number feedback (issue #181): each hit on the enemy raises a number that rises and
    /// fades. It listens to the enemy's own <see cref="CharacterResource.CurrentHasChanged"/>, so the sim is
    /// untouched, and nothing else of the view knows about it: <see cref="EnemyArena"/> binds it behind its
    /// <c>damageNumbers</c> switch and <see cref="EnemyView"/> lets it go. Pruning the feature is deleting
    /// this component, <see cref="DamageNumber"/>, the prefab, and those call sites.
    /// <para>
    /// A coarse frame runs several ticks and each raises the event, so the drops are summed in a
    /// <see cref="DamageAccumulator{TKey}"/> and flushed once per frame: one number per enemy per frame.
    /// The handler reads the event's arguments, never <c>Enemy.Health</c> (the event raises before the value
    /// is written), and only drops count; the accumulator would sum a heal as negative damage.
    /// </para>
    /// <para>
    /// Killing blow: <see cref="Detach"/> stops listening when the enemy falls, but the view keeps drawing
    /// while it fades, so its <c>Update</c> flushes what the blow queued. With the death fade off the view is
    /// released at once and <see cref="Unbind"/> flushes it on the way out - unless the Encounter is gone
    /// (Recall), when the pending amount is dropped. Numbers themselves are <see cref="DamageNumber"/>s in the
    /// arena's space, so they outlive the view's fade and pooling.
    /// </para>
    /// <para>
    /// Subscribed in <see cref="Bind"/> and released in <see cref="Detach"/>/<see cref="Unbind"/>, driven from the
    /// arena's bind path and not from <c>Awake</c>/<c>OnDisable</c>: the scene object survives a Play session
    /// under disabled domain reload (<c>codebase-notes.md</c>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyView))]
    public sealed class EnemyDamageNumbers : MonoBehaviour
    {
        [SerializeField, Tooltip("The figure that rises. It is pooled and parented beside the views, never under one.")]
        private DamageNumber prefab;
        [SerializeField, Tooltip("Canvas units from the view's centre where a number starts.")]
        private Vector2 startOffset = new(0f, 40f);
        [SerializeField, Min(0f), Tooltip("Canvas units a number rises.")]
        private float riseHeight = 60f;
        [SerializeField, Min(0.01f), Tooltip("Sim seconds a number lives. Scales with sim speed and freezes on pause.")]
        private float lifetime = 0.8f;
        [SerializeField, Range(0f, 1f), Tooltip("Fraction of the life it stays opaque before fading out.")]
        private float holdFraction = 0.4f;

        private readonly DamageAccumulator<Enemy> _hits = new();
        private EnemyView _view;
        private PrefabPool<DamageNumber> _pool;
        private CharacterResource _health;
        private Enemy _enemy;
        private RunState _run;
        private EncounterSimulation _encounter;
        private bool _hasPending;

        private EnemyView View => _view ? _view : _view = GetComponent<EnemyView>();

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload.
        private PrefabPool<DamageNumber> Pool => _pool ??= new PrefabPool<DamageNumber>(prefab, transform.parent);

        /// <summary>Whether this component is listening to an enemy's health.</summary>
        public bool IsBound => _health != null;

        /// <summary>Whether hits are waiting for this frame's flush.</summary>
        public bool HasPending => _hasPending;

        /// <summary>Show a number for each frame's worth of damage <paramref name="enemy"/> takes.</summary>
        public void Bind(Enemy enemy)
        {
            Unbind();

            if (prefab == null)
                return;

            _enemy = enemy;
            _run = SimulationService.Instance.Run;
            _encounter = _run.Encounter;
            _health = enemy.HealthResource;
            _health.CurrentHasChanged += OnHealthChanged;
        }

        /// <summary>
        /// Stop listening but keep what is queued: a dying view takes no more hits, yet its killing blow's
        /// number is still to be flushed by <c>Update</c>. Safe when unbound.
        /// </summary>
        public void Detach()
        {
            if (_health == null)
                return;

            _health.CurrentHasChanged -= OnHealthChanged;
            _health = null;
        }

        /// <summary>
        /// Stop listening, show what is still queued if the Encounter is live, and forget everything: a
        /// pooled view starts with no pending damage. Safe when unbound.
        /// </summary>
        public void Unbind()
        {
            Detach();
            Flush();

            // A flush that could not show (not placed, Encounter gone) still leaves nothing behind.
            _hits.Flush(static (_, _) => { });
            _hasPending = false;
            _enemy = null;
            _run = null;
            _encounter = null;
        }

        private void OnHealthChanged(float previous, float current, float total)
        {
            var damage = DamageNumberMotion.Damage(previous, current);
            if (damage <= 0f)
                return;

            _hits.Add(_enemy, damage);
            _hasPending = true;
        }

        private void Update() => Flush();

        /// <summary>One number for everything taken since the last flush. Waits while the view is not yet placed.</summary>
        private void Flush()
        {
            if (!_hasPending)
                return;

            // The view starts at the arena's origin and hidden: a number now would rise from the wrong place.
            if (!View.IsPlaced)
                return;

            _hasPending = false;
            _hits.Flush(ShowNumber);
        }

        private void ShowNumber(Enemy enemy, float amount)
        {
            // The Run ended (Recall, hero death, Relocate) or the arena is going down: nothing is shown.
            if (!isActiveAndEnabled || _run == null || _run.Encounter != _encounter)
                return;

            var origin = ((RectTransform)transform).anchoredPosition + startOffset;
            var number = Pool.GetObject();
            number.Show(origin, amount, lifetime, riseHeight, holdFraction, _encounter, Pool.ReleaseObject);
        }
    }
}
