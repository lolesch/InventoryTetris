using System.Collections.Generic;
using Submodules.Utility.Extensions;
using Submodules.Utility.Tools;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Owns the pooled enemy <see cref="Figure"/>s standing on the sim's arena, drawn around the Hero icon: one per
    /// living enemy of the live Encounter. The hero's figure is the map's own, placed through this arena's projection.
    ///
    /// The simulation owns position (ADR-0018): this only projects it. Each frame every figure is placed at
    /// <see cref="Enemy.Position"/> through an
    /// <see cref="ArenaProjection"/> of <c>arenaScale</c> and <c>tilt</c>, relative to the arena's
    /// <see cref="ArenaTuning.Origin"/>, which is drawn at the anchor. Nothing here walks, rolls or steers.
    ///
    /// Only the <i>binding</i> is polled - <see cref="Update"/> compares the Run's current
    /// <see cref="EncounterSimulation"/> to the one it holds, once a frame. The sim is rebuilt on
    /// every <see cref="RunState.Send"/> and <see cref="RunState.Relocate"/>, and a hero load replaces the Run
    /// itself, so there is no moment to subscribe once and be done; and doing it in one
    /// place in <c>Update</c> leaves no <c>Awake</c>/<c>OnDisable</c> asymmetry to go silently
    /// dead on the second Play entry under disabled domain reload (<c>codebase-notes.md</c>).
    /// Everything else is events: <see cref="EncounterSimulation.EnemySpawned"/> takes a figure,
    /// <see cref="EncounterSimulation.EnemyDefeated"/> retires it (issue #178: the figure fades out for a
    /// short sim-time interval, detached from the enemy, and is pooled when that ends), and each figure
    /// follows its own enemy's health.
    ///
    /// A Death nulls the Encounter, so nothing fires for the live enemies: the arena keeps its figures standing for
    /// the Death beat, then releases them all. A Relocate leaves the Run alive, so the old Encounter's enemies fade
    /// out as defeated ones do.
    ///
    /// The anchor is the active Location's Hero icon (its <see cref="ToggleCheckmark"/> image), resolved every frame
    /// from the Locations' <see cref="ToggleGroup"/>: it moves on a Relocate and goes null once the group resets, and
    /// the group's <c>ActiveMember</c> is not guaranteed to be set when the Run is sent. A null anchor holds the
    /// figures where they are. Positions are in the arena root's space, so this object must sit outside any face
    /// that fades or clips (the minimap's InFields).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyArena : MonoBehaviour, IDamageNumberOrigins, IArenaSpace
    {
        [SerializeField] private Figure prefab;
        [SerializeField, Tooltip("Where the figures live. Empty: this object's own RectTransform.")]
        private RectTransform container;
        [SerializeField, Tooltip("The Locations' toggle group; its active member's Hero icon is the anchor.")]
        private ToggleGroup locations;
        [SerializeField] private EnemyVisuals visuals;
        [SerializeField, Min(0.01f), Tooltip("Canvas units one arena unit spans along the horizontal axis.")]
        private float arenaScale = 16f;
        [SerializeField, Range(0f, 1f), Tooltip("The depth axis against the horizontal one: 1 draws the arena top-down, less flattens it. Drawing only; the rules use arena distances.")]
        private float tilt = 0.65f;
        [SerializeField, Min(0f), Tooltip("Arena units of horizontal offset from the hero under which a figure keeps the way it faces.")]
        private float facingDeadZoneUnits = 0.75f;
        [SerializeField, Tooltip("A defeated enemy fades out before it is pooled. Off: it is released at once.")]
        private bool deathFade = true;
        [SerializeField, Min(0f), Tooltip("Sim seconds the fade takes; scales with sim speed and stops on pause.")]
        private float deathFadeSeconds = 0.5f;
        [SerializeField, Tooltip("Feedback: the sprite flashes white on each hit. Independent of the others; off binds nothing.")]
        private bool hitFlash = true;
        [SerializeField, Tooltip("Feedback: a number rises from whoever is hit - an enemy, or the hero figure - one per target and damage type per frame. Independent of the others; off binds nothing.")]
        private bool damageNumbers = true;
        [SerializeField, Tooltip("The damage numbers' listener and pool. Optional: without it there are no numbers.")]
        private ArenaDamageNumbers numbers;
        [SerializeField, Tooltip("Feedback: the sprite shakes on each hit. Independent of the others; off binds nothing.")]
        private bool hitShake = true;
        [SerializeField, Tooltip("Feedback: a ring marks the target of the hero's latest Strike or Approach Intent. Independent of the others.")]
        private bool targetHighlight = true;

        private readonly Dictionary<Enemy, Figure> _figures = new();
        // Figures whose enemy fell and that are fading out. Not in _figures: no slot, no events, no highlight.
        private readonly List<DyingFigure> _dying = new();
        private readonly List<DepthEntry> _ordered = new();
        private Figure _marked;
        private PrefabPool<Figure> _pool;
        private EncounterSimulation _bound;
        private RunState _run;
        private AbstractToggle _anchorOwner;
        private RectTransform _anchor;
        // Where the arena was last drawn (set by Place), so a number can be projected without a figure to read.
        private Vector2 _center;
        private ArenaProjection _projection;
        private bool _hasCenter;

        // A fallen enemy's figure and how far through its fade it is, in sim seconds.
        private struct DyingFigure
        {
            public Figure Figure;
            public float Elapsed;
        }

        // One drawn figure's place in the depth sort. The tie keeps the order it already has, so equal depth never flickers.
        private readonly struct DepthEntry
        {
            public readonly Transform Transform;
            public readonly Coordinate Arena;
            public readonly int Tie;

            public DepthEntry(Transform transform, Coordinate arena)
            {
                Transform = transform;
                Arena = arena;
                Tie = transform.GetSiblingIndex();
            }
        }

        private RectTransform Root => container != null ? container : (RectTransform)transform;

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload - the scene object survives, only OnEnable/Update repeat.
        private PrefabPool<Figure> Pool => _pool ??= new PrefabPool<Figure>(prefab, Root);

        private void Update()
        {
            var run = SimulationService.Instance.Run;
            var encounter = run.Encounter;

            // A Death ended the Run, but the fight stays on screen, frozen, for the Death beat; its end releases it.
            var frozen = encounter == null && _bound != null && SimulationService.Instance.DeathBeatPlaying;
            if (encounter != _bound && !frozen)
            {
                // A Relocate swaps the Encounter inside one Run: its enemies fade out. Anything else ended the Run.
                Unbind(fadeOut: encounter != null && _bound != null && run == _run);
                if (encounter != null)
                    Bind(encounter);
            }

            _run = run;

            SyncDamageNumbers();

            Place();
            MarkTarget(_bound?.HeroTarget);
            FadeDying(SimulationService.Instance.SimDelta(Time.deltaTime));
        }

        // Update never runs while disabled, so a disabled arena would hold a subscription no one
        // can retire. Letting go here means the first Update after re-enabling binds afresh.
        private void OnDisable() => Unbind();

        private void Bind(EncounterSimulation encounter)
        {
            _bound = encounter;
            _bound.EnemySpawned += OnEnemySpawned;
            _bound.EnemyDefeated += OnEnemyDefeated;

            // The first Encounter's initial batch spawns inside the sim's constructor, before this
            // could listen - and a Send can land between frames with enemies already in. Seeding
            // oldest-first matches what the events would have built.
            for (var i = 0; i < encounter.Enemies.Count; i++)
                SpawnFigure(encounter.Enemies[i]);
        }

        // fadeOut: the live enemies fade as defeated ones do; else the Run ended and nothing lingers.
        private void Unbind(bool fadeOut = false)
        {
            if (_bound != null)
            {
                _bound.EnemySpawned -= OnEnemySpawned;
                _bound.EnemyDefeated -= OnEnemyDefeated;
                _bound = null;
            }

            // Fades already under way go back at once, so the ones started below are the only ones left.
            foreach (var dying in _dying)
                Release(dying.Figure);
            _dying.Clear();

            if (fadeOut && _marked != null)
                _marked.SetHighlighted(false);

            // A sim that ended with enemies alive (hero death, Relocate) raised nothing for them.
            foreach (var figure in _figures.Values)
            {
                if (fadeOut)
                    Retire(figure);
                else
                    Release(figure);
            }

            _figures.Clear();
            _marked = null;

            // No Run, and no number left listening to the sim that ended.
            if (numbers != null)
                numbers.Unbind();
            _hasCenter = false;
        }

        private void OnEnemySpawned(Enemy enemy) => SpawnFigure(enemy);

        private void OnEnemyDefeated(Enemy enemy) => RemoveFigure(enemy);

        private void FadeDying(float simDelta)
        {
            for (var i = _dying.Count; i-- > 0;)
            {
                var dying = _dying[i];
                dying.Elapsed += simDelta;
                _dying[i] = dying;

                if (dying.Figure == null || deathFadeSeconds <= dying.Elapsed)
                {
                    _dying.RemoveAt(i);
                    Release(dying.Figure);
                }
                else
                    dying.Figure.SetAlpha(ArenaLayout.DyingAlpha(dying.Elapsed, deathFadeSeconds));
            }
        }

        /// <summary>
        /// Keeps the damage numbers listening exactly while the switch is on and an Encounter is bound - checked every
        /// frame, so the switch turns the feature off (and back on) mid-Run and a disabled arena leaves nothing subscribed.
        /// </summary>
        private void SyncDamageNumbers()
        {
            if (numbers == null)
                return;

            var wanted = damageNumbers && _bound != null;
            if (wanted && !numbers.IsBound)
                numbers.Bind(_bound, this, Root);
            else if (!wanted && numbers.IsBound)
                numbers.Unbind();
        }

        /// <inheritdoc/>
        public ArenaProjection Projection => new(arenaScale, tilt);

        /// <inheritdoc/>
        public float HeroCanvasSpeed => SimulationService.Instance.HeroWalkingSpeed * arenaScale;

        /// <inheritdoc/>
        public RectTransform LocationAnchor => ResolveAnchor();

        /// <inheritdoc/>
        public bool TryGetNumberOrigin(Coordinate position, out Vector2 origin)
        {
            origin = default;
            if (_bound == null || !_hasCenter)
                return false;

            origin = _center + _projection.ToCanvas(position, _bound.Arena.Origin);
            return true;
        }

        /// <summary>Binds <paramref name="figure"/> to <paramref name="health"/>, and the hit feedbacks behind their switches.</summary>
        private void BindFigure(Figure figure, CharacterResource health, string label, CharacterResource shield = null)
        {
            figure.Bind(health, label, shield);
            if (hitFlash && figure.HitFlash != null)
                figure.HitFlash.Bind(health, figure.Sprite);
            if (hitShake && figure.HitShake != null)
                figure.HitShake.Bind(health);
        }

        /// <summary>Takes a pooled figure for <paramref name="enemy"/>. It stays hidden until <see cref="Place"/> puts it where the sim has the enemy.</summary>
        private void SpawnFigure(Enemy enemy)
        {
            if (_figures.ContainsKey(enemy))
                return;

            var figure = Pool.GetObject();
            figure.Dress(visuals != null ? visuals.For(enemy.Archetype) : EnemyVisuals.Fallback);
            BindFigure(figure, enemy.HealthResource, enemy.Archetype.ToString());
            _figures.Add(enemy, figure);
        }

        /// <summary>
        /// <paramref name="enemy"/> fell: its figure fades out where it stands, then goes back to the pool
        /// (at once when the fade is off). The killing blow's <c>CurrentHasChanged</c> fired before
        /// <c>EnemyDefeated</c>, so whatever it queued on the figure still plays during the fade.
        /// </summary>
        private void RemoveFigure(Enemy enemy)
        {
            if (!_figures.Remove(enemy, out var figure))
                return;

            if (figure == _marked)
                _marked = null;

            Retire(figure);
        }

        // Fades the figure out where it stands, then pools it (at once when the fade is off).
        private void Retire(Figure figure)
        {
            if (figure != null && deathFade && deathFadeSeconds > 0f)
            {
                figure.Detach();
                _dying.Add(new DyingFigure { Figure = figure });
            }
            else
                Release(figure);
        }

        // OnDisable also runs as Play mode tears the scene down, when a figure may already be
        // destroyed - releasing one would throw, and one exception pauses the Editor.
        private void Release(Figure figure)
        {
            if (figure == null)
                return;

            if (figure == _marked)
                _marked = null;

            figure.Unbind();
            Pool.ReleaseObject(figure);
        }

        /// <summary>
        /// Moves the target ring to the figure of <see cref="EncounterSimulation.HeroTarget"/>, the target of the hero's
        /// latest Strike or Approach Intent, and off with no living enemy. Read from <c>_figures</c> only: a dying
        /// enemy has left it, so it is never marked. Nothing is marked with the switch off or no Encounter.
        /// </summary>
        private void MarkTarget(Enemy heroTarget)
        {
            Figure target = null;
            if (targetHighlight && _bound != null && heroTarget != null)
                _figures.TryGetValue(heroTarget, out target);

            if (target == _marked)
                return;

            if (_marked != null)
                _marked.SetHighlighted(false);

            _marked = target;
            if (_marked != null)
                _marked.SetHighlighted(true);
        }

        /// <summary>
        /// Puts every enemy figure where the sim has it, projected from the arena's origin at the anchor, and turns
        /// it toward the hero. Nothing with no Encounter or no anchor.
        /// </summary>
        private void Place()
        {
            if (_bound == null)
                return;

            var anchor = ResolveAnchor();
            _hasCenter = anchor != null;
            if (anchor == null)
                return;

            var root = Root;
            var world = anchor.TransformPoint(anchor.rect.center);
            var center = (Vector2)root.InverseTransformPoint(world) - root.rect.center;

            _center = center;
            _projection = new ArenaProjection(arenaScale, tilt);

            // Enemies face the hero.
            var moved = false;
            foreach (var (enemy, figure) in _figures)
                moved |= PlaceFigure(figure, enemy.Position, _bound.HeroPosition);

            if (moved)
                SortByDepth();
        }

        /// <summary>Puts <paramref name="figure"/> at <paramref name="at"/>, turned toward <paramref name="facing"/> (its way kept when null).</summary>
        private bool PlaceFigure(Figure figure, Coordinate at, Coordinate? facing)
        {
            var sign = facing.HasValue
                ? ArenaLayout.FacingSign(at.x, facing.Value.x, figure.FacingSign, facingDeadZoneUnits)
                : figure.FacingSign;
            return figure.Place(_center + _projection.ToCanvas(at, _bound.Arena.Origin), at, sign);
        }

        /// <summary>The active Location's Hero icon, or null while nothing is selected.</summary>
        private RectTransform ResolveAnchor()
        {
            var member = locations != null ? locations.ActiveMember : null;
            if (member == null)
            {
                _anchorOwner = null;
                _anchor = null;
                return null;
            }

            if (member != _anchorOwner)
            {
                _anchorOwner = member;
                //var checkmark = member.GetComponentInChildren<ToggleCheckmark>(true);
                _anchor = (RectTransform) member.transform; //checkmark != null ? (RectTransform)checkmark.transform : null;
            }

            return _anchor;
        }

        /// <summary>
        /// Nearer draws in front: sibling order by arena depth, the far side first. It reads the arena and not
        /// the canvas, so a tilt of zero still orders the figures.
        /// </summary>
        private void SortByDepth()
        {
            _ordered.Clear();
            foreach (var figure in _figures.Values)
                Order(figure);
            foreach (var dying in _dying)
                Order(dying.Figure);

            _ordered.Sort(CompareDepth);

            for (var i = 0; i < _ordered.Count; i++)
                _ordered[i].Transform.SetSiblingIndex(i);
        }

        // A figure that has not been placed (no hero without a Run) has no place to sort by; a destroyed one is skipped.
        private void Order(Figure figure)
        {
            if (figure != null && figure.IsPlaced)
                _ordered.Add(new DepthEntry(figure.transform, figure.ArenaPosition));
        }

        private static int CompareDepth(DepthEntry a, DepthEntry b)
        {
            var byDepth = ArenaProjection.DepthOrder(a.Arena, b.Arena);
            return byDepth != 0 ? byDepth : a.Tie.CompareTo(b.Tie);
        }
    }
}
