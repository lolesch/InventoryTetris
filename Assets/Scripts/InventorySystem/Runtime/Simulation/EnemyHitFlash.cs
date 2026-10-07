using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The hit flash feedback (issue #179): the enemy's sprite goes white each time its health drops and
    /// ramps back over a short stretch of <i>sim</i> time. It listens to the enemy's own
    /// <see cref="CharacterResource.CurrentHasChanged"/>, so the sim is untouched, and nothing else of the
    /// view knows about it: <see cref="EnemyArena"/> binds it behind its <c>hitFlash</c> switch, and
    /// <see cref="EnemyView.Unbind"/> lets it go. Pruning the feature is deleting this component, its
    /// <c>HitFlash</c> child in the prefab, and those two call sites.
    /// <para>
    /// A <see cref="UnityEngine.UI.Graphic"/> tint can only darken, so the white is a plain Image
    /// (<see cref="fill"/>) clipped to the sprite's silhouette by a <see cref="Mask"/> on a copy of the
    /// sprite (<see cref="silhouette"/>). The flash is that fill's alpha.
    /// </para>
    /// <para>
    /// The health event raises before the value is written, so the handler reads its arguments, never
    /// <c>Enemy.Health</c>. Several hits in one coarse frame call <see cref="HitFlashRamp.Hit"/> several
    /// times and flash once.
    /// </para>
    /// <para>
    /// Subscribed in <see cref="Bind"/> and released in <see cref="Unbind"/>, driven from the arena's
    /// bind path and not from <c>Awake</c>/<c>OnDisable</c>: the scene object survives a Play session
    /// under disabled domain reload (<c>codebase-notes.md</c>).
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHitFlash : MonoBehaviour
    {
        [SerializeField, Tooltip("A copy of the sprite that only shapes the flash: a Mask on it clips the fill to the silhouette.")]
        private Image silhouette;
        [SerializeField, Tooltip("White, a child of the silhouette. Its alpha is the flash.")]
        private Image fill;
        [SerializeField, Min(0.01f), Tooltip("Sim seconds a full flash takes to fade. Scales with sim speed and freezes on pause.")]
        private float duration = 0.15f;

        private readonly HitFlashRamp _ramp = new();
        private CharacterResource _health;

        /// <summary>The flash in flight, 0 when the sprite is normal.</summary>
        public float Intensity => _ramp.Intensity;

        /// <summary>Whether this component is listening to an enemy's health.</summary>
        public bool IsBound => _health != null;

        /// <summary>Flash <paramref name="sprite"/> each time <paramref name="enemy"/>'s health drops.</summary>
        public void Bind(Enemy enemy, Sprite sprite)
        {
            Unbind();

            // No art, no silhouette to flash: a white box over nothing would be the bug.
            if (sprite == null)
                return;

            silhouette.sprite = sprite;
            _health = enemy.HealthResource;
            _health.CurrentHasChanged += OnHealthChanged;
        }

        /// <summary>
        /// Stop listening but let the flash in flight finish: a dying view takes no more hits, yet the
        /// killing blow's flash (raised before the defeat) should still play. Safe when unbound.
        /// </summary>
        public void Detach()
        {
            if (_health == null)
                return;

            _health.CurrentHasChanged -= OnHealthChanged;
            _health = null;
        }

        /// <summary>Stop listening and clear the tint: a pooled view must start un-tinted. Safe when unbound.</summary>
        public void Unbind()
        {
            Detach();

            _ramp.Reset();
            Apply();
        }

        private void OnHealthChanged(float previous, float current, float total)
        {
            if (HitFlashRamp.IsHit(previous, current))
                _ramp.Hit();
        }

        private void Update()
        {
            if (!_ramp.IsActive)
                return;

            _ramp.Advance(SimDelta(), duration);
            Apply();
        }

        /// <summary>The seconds the sim moved this frame, as <c>SimulationService.Tick</c> feeds it: 0 while paused.</summary>
        private static float SimDelta()
        {
            if (SimulationService.Instance.IsPaused)
                return 0f;

            return Time.deltaTime * Mathf.Max(0f, Session.Instance.Hero.Behaviour.SimSpeed);
        }

        private void Apply()
        {
            if (fill == null)
                return;

            var color = fill.color;
            color.a = _ramp.Intensity;
            fill.color = color;
        }
    }
}
