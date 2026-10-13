using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// One fighter's figure in the arena (issues #176, #208, epic #262): a sprite and a health bar, bound to one
    /// health resource at a time. The hero has one scene object and each enemy a pooled one; both are
    /// <see cref="EnemyArena"/>'s, the only intended caller.
    /// <para>
    /// The root is moved by the arena - to where the sim says the fighter stands, projected, never walked by
    /// the figure itself (ADR-0018) - and never flipped. The sprite child is the only thing that is,
    /// because a flipped root would mirror the bar's fill direction and its text.
    /// </para>
    /// <para>
    /// A figure is fully reset on <see cref="Unbind"/>: position, facing, visibility. A stale
    /// <c>scale.x = -1</c> on the next enemy, or on the hero's next Run, is the bug that is designed against.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class Figure : MonoBehaviour
    {
        [SerializeField, Tooltip("The sprite child: the image, and the one thing mirrored to face the way the fighter fights. Art is authored facing right. The hit flash and the hit shake are found under it.")]
        private Image spriteImage;
        [SerializeField, Tooltip("The target ring (#182), off until the arena marks this figure. Optional: it is shown behind the arena's targetHighlight switch.")]
        private Image highlight;

        // The rest are found once in Awake: they sit on this object or under it, so a serialized slot for each
        // only restated the hierarchy. The feedbacks are optional (a prefab may drop one); the bar is not.
        private RectTransform _sprite;
        private HealthBarDisplay _health;
        // Hides a figure that has no position yet, so a fresh one never flashes at the arena's origin.
        private CanvasGroup _visibility;
        private HitFlash _hitFlash;
        private HitShake _hitShake;

        private RectTransform _rect;
        private int _sign = 1;
        private bool _placed;
        private bool _bound;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        // Runs once per instance, as the pool makes it (or as the scene loads, for the hero's): the arena only
        // binds a figure it has activated.
        private void Awake()
        {
            _sprite = spriteImage.rectTransform;
            _health = GetComponentInChildren<HealthBarDisplay>(true);
            _visibility = GetComponent<CanvasGroup>();
            _hitFlash = GetComponentInChildren<HitFlash>(true);
            _hitShake = GetComponentInChildren<HitShake>(true);
        }

        /// <summary>
        /// Where the fighter stood on the arena when the figure was last placed. A dying figure keeps it: the
        /// figure fades out where it fell, and the depth sort still reads it.
        /// </summary>
        public Coordinate ArenaPosition { get; private set; }

        /// <summary>The sprite's <c>scale.x</c> sign: +1 faces right, -1 faces left.</summary>
        public int FacingSign => _sign;

        /// <summary>Whether the figure has been put in the arena since it was bound. Reset in <see cref="Unbind"/>.</summary>
        public bool IsPlaced => _placed;

        /// <summary>Whether the figure follows a health resource: false while pooled, and once it let go to fade out.</summary>
        public bool IsBound => _bound;

        /// <summary>The art the figure shows now; the hit flash's silhouette is a copy of it.</summary>
        public Sprite Sprite => spriteImage.sprite;

        /// <summary>The hit flash feedback, or null when the prefab has none.</summary>
        public HitFlash HitFlash => _hitFlash;

        /// <summary>The hit shake feedback, or null when the prefab has none.</summary>
        public HitShake HitShake => _hitShake;

        /// <summary>Whether the target ring is showing on this figure. Reset in <see cref="Unbind"/>.</summary>
        public bool IsHighlighted => highlight != null && highlight.enabled;

        /// <summary>
        /// Shows or hides the ring that marks the enemy the next Strike hits (#182). A figure that is not bound -
        /// pooled, or dying - is never marked, whatever the caller asks.
        /// </summary>
        public void SetHighlighted(bool on)
        {
            if (highlight != null)
                highlight.enabled = on && _bound;
        }

        /// <summary>Dress as <paramref name="entry"/> says. The hero's figure keeps the art it was authored with.</summary>
        public void Dress(EnemyVisuals.Entry entry)
        {
            spriteImage.sprite = entry.Sprite;
            spriteImage.enabled = entry.Sprite != null;
            Rect.sizeDelta = entry.Size;
        }

        /// <summary>Stand for a fighter whose health is <paramref name="health"/>, under <paramref name="label"/>. Hidden until it is first placed.</summary>
        public void Bind(CharacterResource health, string label)
        {
            _bound = true;
            _health.Bind(health, label);

            // A figure the pool just made has never been through Unbind, so it starts as the prefab was saved:
            // a ring left enabled there would show on every fresh figure, whether or not it is the target.
            SetHighlighted(false);
        }

        /// <summary>
        /// Let go of the health resource so the figure takes no further events from it, but let the flash and the
        /// shake in flight play out: an enemy's killing blow raised them before it fell, and its figure fades out
        /// where it stands. Everything the figure subscribes to on <see cref="Bind"/> is released here, so a
        /// fading figure and a pooled one hold none.
        /// </summary>
        public void Detach()
        {
            _health.Unbind();

            if (_hitFlash != null)
                _hitFlash.Detach();

            if (_hitShake != null)
                _hitShake.Detach();

            _bound = false;
            SetHighlighted(false);
        }

        /// <summary>Let go of the health resource and clear every pooled state: position, facing, flash tint, shake offset, target ring, visibility.</summary>
        public void Unbind()
        {
            Detach();

            // Clears the tint as well: a pooled figure starts un-tinted.
            if (_hitFlash != null)
                _hitFlash.Unbind();

            // Sprite back at rest: a pooled figure starts with zero shake offset.
            if (_hitShake != null)
                _hitShake.Unbind();

            ArenaPosition = default;
            _placed = false;
            _sign = 1;

            // OnDisable also runs as Play mode tears the scene down, when the children may already be gone.
            if (_sprite != null)
                _sprite.localScale = Vector3.one;

            Rect.anchoredPosition = Vector2.zero;
            _visibility.alpha = 0f;
        }

        /// <summary>
        /// Stand at <paramref name="canvasPosition"/> and face <paramref name="facingSign"/>, and show the figure if
        /// this is its first placement. The arena works both out from the sim's arena (ADR-0018); the figure
        /// only keeps them.
        /// </summary>
        /// <param name="canvasPosition">The projected position in the arena's anchored space.</param>
        /// <param name="arenaPosition">Where the fighter stands on the sim's arena; the depth sort reads it.</param>
        /// <param name="facingSign">+1 faces right, -1 faces left.</param>
        /// <returns>Whether the figure moved.</returns>
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

        /// <summary>Fades a placed figure to <paramref name="alpha"/>. An unplaced one is still invisible; leave it so.</summary>
        public void SetAlpha(float alpha)
        {
            if (_placed)
                _visibility.alpha = alpha;
        }
    }
}
