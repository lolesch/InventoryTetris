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
    /// Owns the pooled enemy <see cref="Figure"/>s and the hero's one scene <see cref="Figure"/> standing on the
    /// sim's arena, drawn around the Hero icon, and keeps one figure per living enemy of the live Encounter (issues
    /// #94, #176, #208, #266). It replaces the combat panel's enemy HP bar list: the pooling and the binding are
    /// what <c>EnemyHealthBarPool</c> had. The pool, the death fade and the target ring are the enemies' only.
    ///
    /// The simulation owns position (ADR-0018): this only projects it. Each frame every figure is placed at
    /// <see cref="Enemy.Position"/> (the hero at <see cref="EncounterSimulation.HeroPosition"/>) through an
    /// <see cref="ArenaProjection"/> of <c>arenaScale</c> and <c>tilt</c>, relative to the arena's
    /// <see cref="ArenaTuning.Origin"/>, which is drawn at the anchor. Nothing here walks, rolls or steers.
    ///
    /// Only the <i>binding</i> is polled - <see cref="Update"/> compares the Run's current
    /// <see cref="EncounterSimulation"/> to the one it holds, once a frame. The sim is rebuilt on
    /// every <see cref="RunState.Send"/> and <see cref="RunState.Relocate"/>, and a hero load replaces the Run
    /// itself, so there is no moment to subscribe once and be done; and doing it in one
    /// place in <c>Update</c> leaves no <c>Awake</c>/<c>OnDisable</c> asymmetry to go silently
    /// dead on the second Play entry under disabled domain reload (<c>codebase-notes.md</c>).
    /// Everything else is events: <see cref="EncounterSimulation.EnemySpawned"/> takes a view,
    /// <see cref="EncounterSimulation.EnemyDefeated"/> retires it (issue #178: the view fades out for a
    /// short sim-time interval, detached from the enemy, and is pooled when that ends), and each view
    /// follows its own enemy's health.
    ///
    /// A hero death or an auto-Recall leaves live enemies with nothing firing for them - the Run ending
    /// nulls the Encounter, and that release-all is what empties the arena, dying views included.
    ///
    /// The anchor is the active Location's Hero icon (its <see cref="ToggleCheckmark"/> image), resolved
    /// every frame from the Locations' <see cref="ToggleGroup"/>: it moves on a Relocate and goes null once
    /// the group resets, and whether the group's <c>ActiveMember</c> is already set when the Run is sent is
    /// not guaranteed, so nothing is resolved at bind time. A null anchor holds the views where they are
    /// and shows nothing new. Positions are in the arena root's space, so this object must sit outside any
    /// face that fades or clips (the minimap's InFields). The checkmark itself is never moved; the hero figure
    /// is a separate element.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyArena : MonoBehaviour, IDamageNumberOrigins
    {
        [SerializeField] private Figure prefab;
        [SerializeField, Tooltip("Where the views live. Empty: this object's own RectTransform.")]
        private RectTransform container;
        [SerializeField, Tooltip("The Locations' toggle group; its active member's Hero icon is the anchor.")]
        private ToggleGroup locations;
        [SerializeField] private EnemyVisuals visuals;
        [SerializeField, Tooltip("The hero as a figure on the arena: a separate element, so the Hero icon (the arena's origin) stays put. One scene object, never pooled. Optional.")]
        private Figure heroFigure;
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
        [SerializeField, Tooltip("Feedback: a ring marks the enemy the hero's next Strike hits. Independent of the others.")]
        private bool targetHighlight = true;

        private readonly Dictionary<Enemy, Figure> _views = new();
        // Figures whose enemy fell and that are fading out. Not in _views: no slot, no events, no highlight.
        private readonly List<DyingFigure> _dying = new();
        private readonly List<DepthEntry> _ordered = new();
        private Figure _marked;
        private PrefabPool<Figure> _pool;
        private EncounterSimulation _bound;
        private AbstractToggle _anchorOwner;
        private RectTransform _anchor;
        // Where the arena was last drawn (set by Place), so a number can be projected without a view to read.
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
            var encounter = SimulationService.Instance.Run.Encounter;
            if (encounter != _bound)
            {
                Unbind();
                if (encounter != null)
                    Bind(encounter);
            }

            SyncDamageNumbers();

            // Peeked once a frame: it re-runs the sim's choice on every read.
            var strikeTarget = _bound?.StrikeTarget;
            Place(strikeTarget);
            MarkTarget(strikeTarget);
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

            if (heroFigure != null)
                BindFigure(heroFigure, Session.Instance.Hero.GetResource(StatName.Health), "Hero");

            // The first Encounter's initial batch spawns inside the sim's constructor, before this
            // could listen - and a Send can land between frames with enemies already in. Seeding
            // oldest-first matches what the events would have built.
            for (var i = 0; i < encounter.Enemies.Count; i++)
                SpawnView(encounter.Enemies[i]);
        }

        private void Unbind()
        {
            if (_bound != null)
            {
                _bound.EnemySpawned -= OnEnemySpawned;
                _bound.EnemyDefeated -= OnEnemyDefeated;
                _bound = null;
            }

            // A sim that ended with enemies alive (hero death, auto-Recall) raised nothing for them.
            foreach (var view in _views.Values)
                Release(view);
            _views.Clear();
            _marked = null;

            // No Run, no hero on the arena, and no number left listening to the sim that ended.
            if (heroFigure != null)
                heroFigure.Unbind();
            if (numbers != null)
                numbers.Unbind();
            _hasCenter = false;

            // The Run ended, so no fade lingers: dying figures go back at once too.
            foreach (var dying in _dying)
                Release(dying.Figure);
            _dying.Clear();
        }

        private void OnEnemySpawned(Enemy enemy) => SpawnView(enemy);

        private void OnEnemyDefeated(Enemy enemy) => RemoveView(enemy);

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
        public bool TryGetNumberOrigin(ICombatant target, out Vector2 origin)
        {
            origin = default;
            if (_bound == null || !_hasCenter)
                return false;

            if (target is Enemy enemy)
            {
                origin = _center + _projection.ToCanvas(enemy.Position, _bound.Arena.Origin);
                return true;
            }

            if (!ReferenceEquals(target, _bound.Hero))
                return false;

            origin = _center + _projection.ToCanvas(_bound.HeroPosition, _bound.Arena.Origin);
            return true;
        }

        /// <summary>Binds <paramref name="figure"/> to <paramref name="health"/>, and the hit feedbacks behind their switches.</summary>
        private void BindFigure(Figure figure, CharacterResource health, string label)
        {
            figure.Bind(health, label);
            if (hitFlash && figure.HitFlash != null)
                figure.HitFlash.Bind(health, figure.Sprite);
            if (hitShake && figure.HitShake != null)
                figure.HitShake.Bind(health);
        }

        /// <summary>Takes a pooled figure for <paramref name="enemy"/>. It stays hidden until <see cref="Place"/> puts it where the sim has the enemy.</summary>
        private void SpawnView(Enemy enemy)
        {
            if (_views.ContainsKey(enemy))
                return;

            var figure = Pool.GetObject();
            figure.Dress(visuals != null ? visuals.For(enemy.Archetype) : EnemyVisuals.Fallback);
            BindFigure(figure, enemy.HealthResource, enemy.Archetype.ToString());
            _views.Add(enemy, figure);
        }

        /// <summary>
        /// <paramref name="enemy"/> fell: its figure fades out where it stands, then goes back to the pool
        /// (at once when the fade is off). The killing blow's <c>CurrentHasChanged</c> fired before
        /// <c>EnemyDefeated</c>, so whatever it queued on the figure still plays during the fade.
        /// </summary>
        private void RemoveView(Enemy enemy)
        {
            if (!_views.Remove(enemy, out var figure))
                return;

            if (figure == _marked)
                _marked = null;

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
        /// Moves the target ring to the view of <see cref="EncounterSimulation.StrikeTarget"/>, the hero's sticky
        /// target: it stays on one enemy until it falls (or one comes within reach of a hero whose own is not), and is
        /// null with no living enemy. Read from <c>_views</c> only: a dying
        /// enemy has left it, so it is never marked. Nothing is marked with the switch off or no Encounter.
        /// </summary>
        /// <param name="strikeTarget">This frame's <see cref="EncounterSimulation.StrikeTarget"/>, read once by <see cref="Update"/>.</param>
        private void MarkTarget(Enemy strikeTarget)
        {
            Figure target = null;
            if (targetHighlight && _bound != null && strikeTarget != null)
                _views.TryGetValue(strikeTarget, out target);

            if (target == _marked)
                return;

            if (_marked != null)
                _marked.SetHighlighted(false);

            _marked = target;
            if (_marked != null)
                _marked.SetHighlighted(true);
        }

        /// <summary>
        /// Puts every figure where the sim has it, projected from the arena's origin at the anchor, and turns it
        /// toward the hero (the hero toward <paramref name="strikeTarget"/>, his Strike target as <see cref="Update"/>
        /// read it this frame). Nothing with no Encounter or no anchor.
        /// </summary>
        private void Place(Enemy strikeTarget)
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

            // Enemies face the hero; the hero faces his Strike target, and keeps his way with none.
            var moved = false;
            foreach (var (enemy, figure) in _views)
                moved |= PlaceFigure(figure, enemy.Position, _bound.HeroPosition);

            if (heroFigure != null)
                moved |= PlaceFigure(heroFigure, _bound.HeroPosition, strikeTarget?.Position);

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
            foreach (var figure in _views.Values)
                Order(figure);
            foreach (var dying in _dying)
                Order(dying.Figure);
            Order(heroFigure);

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
