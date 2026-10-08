using System.Collections.Generic;
using Submodules.Utility.Extensions;
using Submodules.Utility.Tools;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Owns the pooled <see cref="EnemyView"/> figures and the <see cref="HeroFigure"/> standing on the sim's
    /// ground, drawn around the Hero icon, and keeps one view per living enemy of the live Encounter (issues #94,
    /// #176, #208). It replaces the combat panel's enemy HP bar list: the pooling and the binding are what
    /// <c>EnemyHealthBarPool</c> had.
    ///
    /// The simulation owns position (ADR-0018): this only projects it. Each frame every figure is placed at
    /// <see cref="Enemy.Position"/> (the hero at <see cref="EncounterSimulation.HeroPosition"/>) through an
    /// <see cref="ArenaProjection"/> of <c>groundScale</c> and <c>tilt</c>, relative to the ground's
    /// <see cref="GroundTuning.Origin"/>, which is drawn at the anchor. Nothing here walks, rolls or steers.
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
        [SerializeField] private EnemyView prefab;
        [SerializeField, Tooltip("Where the views live. Empty: this object's own RectTransform.")]
        private RectTransform container;
        [SerializeField, Tooltip("The Locations' toggle group; its active member's Hero icon is the anchor.")]
        private ToggleGroup locations;
        [SerializeField] private EnemyVisuals visuals;
        [SerializeField, Tooltip("The hero as a figure on the ground: a separate element, so the Hero icon (the ground's origin) stays put. Optional.")]
        private HeroFigure heroFigure;
        [SerializeField, Min(0.01f), Tooltip("Canvas units one ground unit spans along the horizontal axis.")]
        private float groundScale = 16f;
        [SerializeField, Range(0f, 1f), Tooltip("The depth axis against the horizontal one: 1 draws the ground top-down, less flattens it. Drawing only; the rules use ground distances.")]
        private float tilt = 0.65f;
        [SerializeField, Min(0f), Tooltip("Ground units of horizontal offset from the hero under which a figure keeps the way it faces.")]
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

        private readonly Dictionary<Enemy, EnemyView> _views = new();
        // Views whose enemy fell and that are fading out. Not in _views: no slot, no events, no highlight.
        private readonly List<EnemyView> _dying = new();
        private readonly List<DepthEntry> _ordered = new();
        private EnemyView _marked;
        private PrefabPool<EnemyView> _pool;
        private EncounterSimulation _bound;
        private AbstractToggle _anchorOwner;
        private RectTransform _anchor;
        // Where the ground was last drawn (set by Place), so a number can be projected without a view to read.
        private Vector2 _center;
        private ArenaProjection _projection;
        private bool _hasCenter;

        // One drawn figure's place in the depth sort. The tie keeps the order it already has, so equal depth never flickers.
        private readonly struct DepthEntry
        {
            public readonly Transform Transform;
            public readonly Coordinate Ground;
            public readonly int Tie;

            public DepthEntry(Transform transform, Coordinate ground)
            {
                Transform = transform;
                Ground = ground;
                Tie = transform.GetSiblingIndex();
            }
        }

        private RectTransform Root => container != null ? container : (RectTransform)transform;

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload - the scene object survives, only OnEnable/Update repeat.
        private PrefabPool<EnemyView> Pool => _pool ??= new PrefabPool<EnemyView>(prefab, Root);

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
            Place();
            MarkTarget();
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

            // No Run, no hero on the ground, and no number left listening to the sim that ended.
            if (heroFigure != null)
                heroFigure.Hide();
            if (numbers != null)
                numbers.Unbind();
            _hasCenter = false;

            // The Run ended, so no fade lingers: dying views go back at once too.
            foreach (var view in _dying)
                Release(view);
            _dying.Clear();
        }

        private void OnEnemySpawned(Enemy enemy) => SpawnView(enemy);

        private void OnEnemyDefeated(Enemy enemy) => RemoveView(enemy);

        private void FadeDying(float simDelta)
        {
            for (var i = _dying.Count; i-- > 0;)
            {
                var view = _dying[i];
                if (view == null || view.AdvanceDying(simDelta))
                {
                    _dying.RemoveAt(i);
                    Release(view);
                }
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
                origin = _center + _projection.ToCanvas(enemy.Position, _bound.Ground.Origin);
                return true;
            }

            if (!ReferenceEquals(target, _bound.Hero))
                return false;

            origin = heroFigure != null && heroFigure.IsShown
                ? heroFigure.CanvasPosition
                : _center + _projection.ToCanvas(_bound.HeroPosition, _bound.Ground.Origin);
            return true;
        }

        /// <summary>Takes a pooled view for <paramref name="enemy"/>. It stays hidden until <see cref="Place"/> puts it where the sim has the enemy.</summary>
        private void SpawnView(Enemy enemy)
        {
            if (_views.ContainsKey(enemy))
                return;

            var entry = visuals != null ? visuals.For(enemy.Archetype) : EnemyVisuals.Fallback;

            var view = Pool.GetObject();
            view.Bind(enemy, entry);
            if (hitFlash && view.HitFlash != null)
                view.HitFlash.Bind(enemy, entry.Sprite);
            if (hitShake && view.HitShake != null)
                view.HitShake.Bind(enemy);
            _views.Add(enemy, view);
        }

        /// <summary>
        /// <paramref name="enemy"/> fell: its view fades out where it stands, then goes back to the pool
        /// (at once when the fade is off). The killing blow's <c>CurrentHasChanged</c> fired before
        /// <c>EnemyDefeated</c>, so whatever it queued on the view still plays during the fade.
        /// </summary>
        private void RemoveView(Enemy enemy)
        {
            if (!_views.Remove(enemy, out var view))
                return;

            if (view == _marked)
                _marked = null;

            if (view != null && deathFade && deathFadeSeconds > 0f)
            {
                view.BeginDying(deathFadeSeconds);
                _dying.Add(view);
            }
            else
                Release(view);
        }

        // OnDisable also runs as Play mode tears the scene down, when a view may already be
        // destroyed - releasing one would throw, and one exception pauses the Editor.
        private void Release(EnemyView view)
        {
            if (view == null)
                return;

            if (view == _marked)
                _marked = null;

            view.Unbind();
            Pool.ReleaseObject(view);
        }

        /// <summary>
        /// Moves the target ring to the view of <see cref="EncounterSimulation.StrikeTarget"/>, which changes as
        /// health does, after a kill, and is null with no living enemy. Read from <c>_views</c> only: a dying
        /// enemy has left it, so it is never marked. Nothing is marked with the switch off or no Encounter.
        /// </summary>
        private void MarkTarget()
        {
            EnemyView target = null;
            if (targetHighlight && _bound != null)
            {
                var enemy = _bound.StrikeTarget;
                if (enemy != null)
                    _views.TryGetValue(enemy, out target);
            }

            if (target == _marked)
                return;

            if (_marked != null)
                _marked.SetHighlighted(false);

            _marked = target;
            if (_marked != null)
                _marked.SetHighlighted(true);
        }

        /// <summary>
        /// Puts every figure where the sim has it, projected from the ground's origin at the anchor, and turns it
        /// toward the hero (the hero toward his Strike target). Nothing with no Encounter or no anchor.
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
            _projection = new ArenaProjection(groundScale, tilt);
            var projection = _projection;
            var origin = _bound.Ground.Origin;
            var hero = _bound.HeroPosition;

            var moved = false;
            foreach (var (enemy, view) in _views)
            {
                var sign = ArenaLayout.FacingSign(enemy.Position.x, hero.x, view.FacingSign, facingDeadZoneUnits);
                moved |= view.Place(center + projection.ToCanvas(enemy.Position, origin), enemy.Position, sign);
            }

            if (heroFigure != null)
            {
                var target = _bound.StrikeTarget;
                var sign = target != null
                    ? ArenaLayout.FacingSign(hero.x, target.Position.x, heroFigure.FacingSign, facingDeadZoneUnits)
                    : heroFigure.FacingSign;
                moved |= heroFigure.Place(center + projection.ToCanvas(hero, origin), hero, sign);
            }

            if (moved)
                SortByDepth();
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
                var checkmark = member.GetComponentInChildren<ToggleCheckmark>(true);
                _anchor = checkmark != null ? (RectTransform)checkmark.transform : null;
            }

            return _anchor;
        }

        /// <summary>
        /// Nearer draws in front: sibling order by ground depth, the far side first. It reads the ground and not
        /// the canvas, so a tilt of zero still orders the figures.
        /// </summary>
        private void SortByDepth()
        {
            _ordered.Clear();
            foreach (var view in _views.Values)
                _ordered.Add(new DepthEntry(view.transform, view.GroundPosition));
            foreach (var view in _dying)
                if (view != null)
                    _ordered.Add(new DepthEntry(view.transform, view.GroundPosition));
            if (heroFigure != null && heroFigure.IsShown)
                _ordered.Add(new DepthEntry(heroFigure.transform, heroFigure.GroundPosition));

            _ordered.Sort(CompareDepth);

            for (var i = 0; i < _ordered.Count; i++)
                _ordered[i].Transform.SetSiblingIndex(i);
        }

        private static int CompareDepth(DepthEntry a, DepthEntry b)
        {
            var byDepth = ArenaProjection.DepthOrder(a.Ground, b.Ground);
            return byDepth != 0 ? byDepth : a.Tie.CompareTo(b.Tie);
        }
    }
}
