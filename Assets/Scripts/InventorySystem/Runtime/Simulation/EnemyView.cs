using Submodules.Utility.Extensions;
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
    /// The root is moved by the arena - to where the sim says the enemy stands, projected, never walked by
    /// the view itself (ADR-0018) - and never flipped. The sprite child is the only thing that is,
    /// because a flipped root would mirror the bar's fill direction and its text.
    /// </para>
    /// <para>
    /// A pooled view is fully reset on <see cref="Unbind"/>: position, facing, visibility. A stale
    /// <c>scale.x = -1</c> on the next enemy is the bug that is designed against.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField, Tooltip("The sprite child: the image, and the one thing mirrored to face the hero. The hit flash and the hit shake are found under it.")]
        private Image spriteImage;
        [SerializeField, Tooltip("The target ring (#182), off until the arena marks this view. Optional: it is shown behind the arena's targetHighlight switch.")]
        private Image highlight;

        // The rest are found once in Awake: they sit on this object or under it, so a serialized slot for each
        // only restated the hierarchy. The feedbacks are optional (a prefab may drop one); the bar is not.
        private RectTransform _sprite;
        private EnemyHealthBarDisplay _health;
        // Hides a view that has no position yet, so a fresh one never flashes at the arena's origin.
        private CanvasGroup _visibility;
        private EnemyHitFlash _hitFlash;
        private EnemyHitShake _hitShake;

        private RectTransform _rect;
        private int _sign = 1;
        private float _dyingElapsed;
        private float _dyingDuration;
        private bool _placed;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        // Runs once per instance, as the pool makes it: the arena only binds a view it has activated.
        private void Awake()
        {
            _sprite = spriteImage.rectTransform;
            _health = GetComponentInChildren<EnemyHealthBarDisplay>(true);
            _visibility = GetComponent<CanvasGroup>();
            _hitFlash = GetComponentInChildren<EnemyHitFlash>(true);
            _hitShake = GetComponentInChildren<EnemyHitShake>(true);
        }

        /// <summary>The enemy this view stands for; null while pooled.</summary>
        public Enemy Enemy { get; private set; }

        /// <summary>
        /// Where the enemy stood on the arena when the view was last placed. A dying view keeps it: only the
        /// link to the enemy is cut, so the figure fades out where it fell, and the depth sort still reads it.
        /// </summary>
        public Coordinate ArenaPosition { get; private set; }

        /// <summary>The sprite's <c>scale.x</c> sign: +1 faces right, -1 faces left.</summary>
        public int FacingSign => _sign;

        /// <summary>Whether the enemy fell and the view is fading out. Never a target for the highlight.</summary>
        public bool IsDying { get; private set; }

        /// <summary>Whether the view has been put in the arena since it was bound. Reset in <see cref="Unbind"/>.</summary>
        public bool IsPlaced => _placed;

        /// <summary>The hit flash feedback, or null when the prefab has none.</summary>
        public EnemyHitFlash HitFlash => _hitFlash;

        /// <summary>The hit shake feedback, or null when the prefab has none.</summary>
        public EnemyHitShake HitShake => _hitShake;

        /// <summary>Whether the target ring is showing on this view. Reset in <see cref="Unbind"/>.</summary>
        public bool IsHighlighted => highlight != null && highlight.enabled;

        /// <summary>
        /// Shows or hides the ring that marks the enemy the next Strike hits (#182). A view that is not bound to
        /// a living enemy - pooled, or dying - is never marked, whatever the caller asks.
        /// </summary>
        public void SetHighlighted(bool on)
        {
            if (highlight != null)
                highlight.enabled = on && Enemy != null && !IsDying;
        }

        /// <summary>Stand for <paramref name="enemy"/>, dressed as <paramref name="entry"/> says. Hidden until it is first placed.</summary>
        public void Bind(Enemy enemy, EnemyVisuals.Entry entry)
        {
            Enemy = enemy;

            spriteImage.sprite = entry.Sprite;
            spriteImage.enabled = entry.Sprite != null;
            Rect.sizeDelta = entry.Size;
            _health.Bind(enemy);

            // A view the pool just made has never been through Unbind, so it starts as the prefab was saved:
            // a ring left enabled there would show on every fresh view, whether or not it is the target.
            SetHighlighted(false);
        }

        /// <summary>
        /// The enemy fell: let go of it and fade out over <paramref name="duration"/> sim seconds, in
        /// place. Whatever the enemy's last hit queued (flash, shake) may still play, because only the
        /// link to the enemy is cut, not the view's own state. The arena drives <see cref="AdvanceDying"/>
        /// and releases the view when that reports it finished.
        /// </summary>
        public void BeginDying(float duration)
        {
            Detach();
            SetHighlighted(false);
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

            // A view that died before it was ever put in the arena is still invisible; leave it so.
            if (IsPlaced)
                _visibility.alpha = ArenaLayout.DyingAlpha(_dyingElapsed, _dyingDuration);

            return _dyingElapsed >= _dyingDuration;
        }

        // Cuts the link to the enemy so the view takes no further events from it. Everything the view
        // subscribes to on Bind is released here, so a dying view and a pooled one hold none.
        private void Detach()
        {
            _health.Unbind();

            // Only the listening stops: the flash a killing blow just queued plays out while dying.
            if (_hitFlash != null)
                _hitFlash.Detach();

            // Same for the shake: the killing blow's jolt settles on its own.
            if (_hitShake != null)
                _hitShake.Detach();

            Enemy = null;
        }

        /// <summary>Let go of the enemy and clear every pooled state: position, facing, flash tint, shake offset, target ring, visibility, dying.</summary>
        public void Unbind()
        {
            Detach();

            // Clears the tint as well: a pooled view starts un-tinted.
            if (_hitFlash != null)
                _hitFlash.Unbind();

            // Sprite back at rest: a pooled view starts with zero shake offset.
            if (_hitShake != null)
                _hitShake.Unbind();
            SetHighlighted(false);

            ArenaPosition = default;
            IsDying = false;
            _dyingElapsed = 0f;
            _dyingDuration = 0f;
            _placed = false;
            _sign = 1;

            _sprite.localScale = Vector3.one;

            Rect.anchoredPosition = Vector2.zero;
            _visibility.alpha = 0f;
        }

        /// <summary>
        /// Stand at <paramref name="canvasPosition"/> and face <paramref name="facingSign"/>, and show the view if
        /// this is its first placement. The arena works both out from the sim's arena (ADR-0018); the view
        /// only keeps them.
        /// </summary>
        /// <param name="canvasPosition">The projected position in the arena's anchored space.</param>
        /// <param name="arenaPosition">Where the enemy stands on the sim's arena; the depth sort reads it.</param>
        /// <param name="facingSign">+1 faces right, -1 faces left.</param>
        /// <returns>Whether the view moved.</returns>
        public bool Place(Vector2 canvasPosition, Coordinate arenaPosition, int facingSign)
        {
            var moved = !_placed || (Rect.anchoredPosition - canvasPosition).sqrMagnitude > 1e-6f;
            if (moved)
                Rect.anchoredPosition = canvasPosition;
            ArenaPosition = arenaPosition;

            _sign = facingSign;
            if (!Mathf.Approximately(_sprite.localScale.x, _sign))
                _sprite.localScale = new Vector3(_sign, 1f, 1f);

            _placed = true;
            _visibility.alpha = 1f;

            return moved;
        }
    }
}
