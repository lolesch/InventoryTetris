using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// One enemy figure in the arena (issue #176): a sprite and the existing health bar, bound to one
    /// <see cref="Enemy"/> at a time and pooled by <see cref="EnemyArena"/>, the only intended caller.
    /// <para>
    /// The root is moved by the arena and never flipped. The sprite child is the only thing that is,
    /// because a flipped root would mirror the bar's fill direction and its text.
    /// </para>
    /// <para>
    /// A pooled view is fully reset on <see cref="Unbind"/>: position, facing, visibility, walk-in. A stale
    /// <c>scale.x = -1</c> on the next enemy is the bug that is designed against.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField, Tooltip("Child that is mirrored to face the hero. Nothing else is.")]
        private RectTransform sprite;
        [SerializeField] private Image spriteImage;
        [SerializeField] private EnemyHealthBarDisplay health;
        [SerializeField, Tooltip("Hides a view that has no position yet, so a fresh one never flashes at the arena's origin.")]
        private CanvasGroup visibility;
        [SerializeField, Tooltip("The hit flash feedback (#179). Optional: the arena binds it behind its hitFlash switch.")]
        private EnemyHitFlash hitFlash;

        private RectTransform _rect;
        private EnemyVisuals.Entry _entry;
        private int _sign = 1;
        private ArenaWalk _walk;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        /// <summary>The enemy this view stands for; null while pooled.</summary>
        public Enemy Enemy { get; private set; }

        /// <summary>Where on the ring this view stands, radians. Picked once at spawn and kept until it falls.</summary>
        public float SlotAngle { get; private set; }

        /// <summary>The sprite's <c>scale.x</c> sign: +1 faces right, -1 faces left.</summary>
        public int FacingSign => _sign;

        /// <summary>Whether the view has been put in the arena since it was bound: on the walk-in's start or beyond.</summary>
        public bool IsPlaced => _walk.Started;

        /// <summary>Whether the walk-in has reached the ring since the view was bound. Reset in <see cref="Unbind"/>.</summary>
        public bool HasArrived => _walk.Arrived;

        /// <summary>The hit flash feedback, or null when the prefab has none.</summary>
        public EnemyHitFlash HitFlash => hitFlash;

        /// <summary>Stand for <paramref name="enemy"/> on the ring at <paramref name="slotAngle"/>, dressed as <paramref name="entry"/> says.</summary>
        public void Bind(Enemy enemy, EnemyVisuals.Entry entry, float slotAngle)
        {
            Enemy = enemy;
            _entry = entry;
            SlotAngle = slotAngle;

            spriteImage.sprite = entry.Sprite;
            spriteImage.enabled = entry.Sprite != null;
            Rect.sizeDelta = entry.Size;
            health.Bind(enemy);
        }

        /// <summary>Let go of the enemy and clear every pooled state: position, facing, flash tint, visibility, the walk-in.</summary>
        public void Unbind()
        {
            if (health != null)
                health.Unbind();

            if (hitFlash != null)
                hitFlash.Unbind();

            Enemy = null;
            SlotAngle = 0f;
            _walk = default;
            _sign = 1;

            if (sprite != null)
                sprite.localScale = Vector3.one;

            Rect.anchoredPosition = Vector2.zero;
            visibility.alpha = 0f;
        }

        /// <summary>
        /// Walk the view in toward <paramref name="anchor"/> and turn it toward it. A view not yet placed
        /// starts <paramref name="spawnMargin"/> beyond its slot and shows itself; it then walks straight
        /// at the anchor at its archetype's speed and stops on the ring, where it stays on its slot.
        /// </summary>
        /// <param name="anchor">The hero anchor in the arena's anchored space.</param>
        /// <param name="deadZone">Horizontal distance under which the facing is kept, so an enemy straight
        /// above or below the hero does not flicker.</param>
        /// <param name="simDelta">The sim's delta for this frame (wall delta times sim speed; 0 while paused).</param>
        /// <param name="spawnMargin">Canvas units beyond the ring a new view starts at.</param>
        /// <returns>Whether the view moved.</returns>
        public bool PlaceAround(Vector2 anchor, float deadZone, float simDelta, float spawnMargin)
        {
            var wasPlaced = IsPlaced;
            if (!wasPlaced)
                _walk = ArenaWalk.Begin(SlotAngle, _entry.RingRadiusX, _entry.RingRadiusY, spawnMargin);

            _walk.Step(_entry.ApproachSpeed * Mathf.Max(0f, simDelta), SlotAngle, _entry.RingRadiusX, _entry.RingRadiusY);

            var position = anchor + _walk.Offset;
            var moved = !wasPlaced || (Rect.anchoredPosition - position).sqrMagnitude > 1e-6f;
            if (moved)
                Rect.anchoredPosition = position;

            _sign = ArenaLayout.FacingSign(position.x, anchor.x, _sign, deadZone);
            if (!Mathf.Approximately(sprite.localScale.x, _sign))
                sprite.localScale = new Vector3(_sign, 1f, 1f);

            visibility.alpha = 1f;

            return moved;
        }
    }
}
