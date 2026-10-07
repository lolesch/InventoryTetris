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
        [SerializeField, Tooltip("The damage number feedback (#181). Optional: the arena binds it behind its damageNumbers switch.")]
        private EnemyDamageNumbers damageNumbers;

        private RectTransform _rect;
        private EnemyVisuals.Entry _entry;
        private int _sign = 1;
        private float _dyingElapsed;
        private float _dyingDuration;
        private ArenaWalk _walk;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        /// <summary>The enemy this view stands for; null while pooled.</summary>
        public Enemy Enemy { get; private set; }

        /// <summary>Where on the ring this view stands, radians. Picked once at spawn and kept until it falls.</summary>
        public float SlotAngle { get; private set; }

        /// <summary>The sprite's <c>scale.x</c> sign: +1 faces right, -1 faces left.</summary>
        public int FacingSign => _sign;

        /// <summary>Whether the enemy fell and the view is fading out. Never a target for the highlight.</summary>
        public bool IsDying { get; private set; }

        /// <summary>Whether the view has been put in the arena since it was bound: on the walk-in's start or beyond.</summary>
        public bool IsPlaced => _walk.Started;

        /// <summary>Whether the walk-in has reached the ring since the view was bound. Reset in <see cref="Unbind"/>.</summary>
        public bool HasArrived => _walk.Arrived;

        /// <summary>The hit flash feedback, or null when the prefab has none.</summary>
        public EnemyHitFlash HitFlash => hitFlash;

        /// <summary>The damage number feedback, or null when the prefab has none.</summary>
        public EnemyDamageNumbers DamageNumbers => damageNumbers;

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

        /// <summary>
        /// The enemy fell: let go of it and fade out over <paramref name="duration"/> sim seconds, in
        /// place. Whatever the enemy's last hit queued (flash, number) may still play, because only the
        /// link to the enemy is cut, not the view's own state. The arena drives <see cref="AdvanceDying"/>
        /// and releases the view when that reports it finished.
        /// </summary>
        public void BeginDying(float duration)
        {
            Detach();
            IsDying = true;
            _dyingElapsed = 0f;
            _dyingDuration = duration;
        }

        /// <summary>Moves the fade on by <paramref name="simDelta"/> sim seconds; true once it is over.</summary>
        public bool AdvanceDying(float simDelta)
        {
            if (!IsDying)
                return false;

            _dyingElapsed += simDelta;

            // A view that died before it was ever put on the ring is still invisible; leave it so.
            if (IsPlaced)
                visibility.alpha = ArenaLayout.DyingAlpha(_dyingElapsed, _dyingDuration);

            return _dyingElapsed >= _dyingDuration;
        }

        // Cuts the link to the enemy so the view takes no further events from it. Everything the view
        // subscribes to on Bind is released here, so a dying view and a pooled one hold none.
        private void Detach()
        {
            if (health != null)
                health.Unbind();

            // Only the listening stops: the flash a killing blow just queued plays out while dying.
            if (hitFlash != null)
                hitFlash.Detach();

            // Likewise only the listening: the killing blow's number is flushed while the view fades.
            if (damageNumbers != null)
                damageNumbers.Detach();

            Enemy = null;
        }

        /// <summary>Let go of the enemy and clear every pooled state: position, facing, flash tint, visibility, the walk-in, dying.</summary>
        public void Unbind()
        {
            Detach();

            // Clears the tint as well: a pooled view starts un-tinted.
            if (hitFlash != null)
                hitFlash.Unbind();

            // Shows what a release without a fade still owes, then drops the rest: no pending damage is pooled.
            if (damageNumbers != null)
                damageNumbers.Unbind();

            SlotAngle = 0f;
            IsDying = false;
            _dyingElapsed = 0f;
            _dyingDuration = 0f;
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
