using Submodules.Utility.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The hero as a figure standing on the arena (spatial-combat spec, issue #208). It is a separate element
    /// from the Hero icon: the icon is the selected Location's marker and the arena's origin, and stays put,
    /// while this is what the hero's own position moves (issue #209) and what his incoming damage numbers rise
    /// at (issue #213).
    /// <para>
    /// Like <see cref="EnemyView"/> the root is moved by <see cref="EnemyArena"/> and never flipped; the sprite
    /// child is the only thing mirrored, so anything later put on the root (a bar, a number origin) keeps its
    /// direction. It is hidden until the arena places it and hidden again when the Run ends. It has no sim
    /// state of its own: the arena works the position and the facing out from the sim and hands them in.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class HeroFigure : MonoBehaviour
    {
        [SerializeField, Tooltip("The sprite child: the image, and the one thing mirrored to face the way the hero fights. Art is authored facing right.")]
        private Image spriteImage;

        private CanvasGroup _visibility;
        private RectTransform _rect;
        private RectTransform _sprite;
        private int _sign = 1;

        private CanvasGroup Visibility => _visibility ? _visibility : _visibility = GetComponent<CanvasGroup>();
        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;
        private RectTransform Sprite => _sprite ? _sprite : _sprite = spriteImage.rectTransform;

        /// <summary>The root's position in the arena's anchored space; where #213's incoming numbers rise from.</summary>
        public Vector2 CanvasPosition => Rect.anchoredPosition;

        /// <summary>Where the hero stood on the arena when the figure was last placed; the depth sort reads it.</summary>
        public Coordinate ArenaPosition { get; private set; }

        /// <summary>The sprite's <c>scale.x</c> sign: +1 faces right, -1 faces left.</summary>
        public int FacingSign => _sign;

        /// <summary>Whether the arena has placed the figure and not hidden it since.</summary>
        public bool IsShown { get; private set; }

        /// <summary>Stand at <paramref name="canvasPosition"/> facing <paramref name="facingSign"/>, and show the figure. Returns whether it moved or appeared.</summary>
        public bool Place(Vector2 canvasPosition, Coordinate arenaPosition, int facingSign)
        {
            var moved = !IsShown || (Rect.anchoredPosition - canvasPosition).sqrMagnitude > 1e-6f;
            if (moved)
                Rect.anchoredPosition = canvasPosition;
            ArenaPosition = arenaPosition;

            _sign = facingSign;
            if (!Mathf.Approximately(Sprite.localScale.x, _sign))
                Sprite.localScale = new Vector3(_sign, 1f, 1f);

            IsShown = true;
            Visibility.alpha = 1f;

            return moved;
        }

        /// <summary>No Run, no hero on the arena: invisible and reset, so the next Run's figure starts facing right.</summary>
        public void Hide()
        {
            IsShown = false;
            ArenaPosition = default;
            _sign = 1;

            // OnDisable also runs as Play mode tears the scene down, when the children may already be gone.
            if (spriteImage != null)
                Sprite.localScale = Vector3.one;
            Visibility.alpha = 0f;
        }
    }
}
