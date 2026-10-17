using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Field map's own hero <see cref="Figure"/> (issues #316, #323, epic #344): one scene object, never pooled,
    /// owned by the map rather than bound by <see cref="EnemyArena"/>. It follows the hero for as long as the map is
    /// up and fades with it. It has three ways of being drawn: standing at an anchor (the Field Exit when the map comes
    /// up), walking to one (Travel, ADR-0021: shown, never simulated, so Pause leaves it walking), and following the
    /// fight, where it stands where the simulation has the hero (ADR-0018), projected by the arena that draws the enemies.
    ///
    /// A Send walks it from the Field Exit to the Location, then it follows the fight. The Encounter is not held for
    /// the walk. Like the arena, only the binding is polled: the Run's current Encounter against the one held, once a
    /// frame, and a phase change on the same Run is what tells a Send from a hero load that brought a Run in
    /// mid-fight. It updates after the arena in the same frame, so the arena has placed its own figures first.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Figure))]
    public sealed class HeroFigure : MonoBehaviour, IHeroFigure
    {
        // Canvas units of horizontal distance from the goal under which a walking figure keeps the way it faces.
        private const float WalkFacingDeadZone = 1f;

        [SerializeField, Tooltip("Projects the hero's arena position onto this map and says where the selected Location is. Must share this figure's parent.")]
        private EnemyArena arena;
        [SerializeField, Min(0f), Tooltip("Arena units of horizontal offset from the hero's target under which the figure keeps the way it faces.")]
        private float facingDeadZoneUnits = 0.75f;
        [SerializeField, Tooltip("Where the figure stands when the Field map comes up.")]
        private RectTransform fieldExit;
        [SerializeField, Tooltip("The Field map's canvas group. The figure fades with it. Empty: always opaque.")]
        private CanvasGroup map;

        private enum Mode
        {
            Standing,
            Walking,
            Following,
        }

        private readonly IHeroTravel _travel = new HeroTravel(default);
        private Figure _figure;
        private Hero _hero;
        private EncounterSimulation _bound;
        private RunState _run;
        private RunPhase _phase;
        private Mode _mode;
        private RectTransform _standAt;
        private RectTransform _target;
        private bool _awaitingLocation;

        private Figure Figure => _figure ? _figure : _figure = GetComponent<Figure>();

        private IDamageNumberOrigins Projection => arena;

        private IArenaSpace ArenaSpace => arena;

        /// <inheritdoc/>
        public bool IsWalking => _mode == Mode.Walking;

        /// <inheritdoc/>
        public void StandAt(RectTransform anchor)
        {
            _standAt = anchor;
            _target = null;
            _awaitingLocation = false;
            _mode = Mode.Standing;
        }

        /// <inheritdoc/>
        public void TravelTo(RectTransform anchor)
        {
            var goal = LocalCentre(anchor);
            var from = Figure.IsPlaced ? ((RectTransform)transform).anchoredPosition : goal;

            _travel.Place(new Coordinate(from));
            _travel.Retarget(new Coordinate(goal));
            _target = anchor;
            _awaitingLocation = false;
            _mode = Mode.Walking;
        }

        private void Update()
        {
            var run = SimulationService.Instance.Run;
            var sent = run == _run && _phase == RunPhase.InTown && run.Phase == RunPhase.InField;
            _run = run;
            _phase = run.Phase;

            SyncHero();
            SyncEncounter(run.Encounter, sent);

            // A Send's walk starts once the Location's anchor can be read, as the arena waits for it too.
            if (_awaitingLocation && ArenaSpace.LocationAnchor != null)
                TravelTo(ArenaSpace.LocationAnchor);

            switch (_mode)
            {
                case Mode.Walking: Walk(); break;
                case Mode.Following: Follow(); break;
                default: Stand(); break;
            }

            Figure.SetAlpha(map != null ? map.alpha : 1f);
        }

        // Update never runs while disabled, so a disabled map would hold a figure no one can retire.
        private void OnDisable()
        {
            if (_hero != null)
                Figure.Unbind();

            _hero = null;
            _bound = null;
            _run = null;
            _phase = default;
            StandAt(null);
        }

        // The figure is the hero's for as long as the map is up, with or without a Run; a hero load swaps the hero.
        private void SyncHero()
        {
            var hero = Session.Instance.Hero;
            if (hero == _hero)
                return;

            if (_hero != null)
                Figure.Unbind();

            _hero = hero;
            var health = hero.GetResource(StatName.Health);
            Figure.Bind(health, "Hero", hero.GetResource(StatName.Shield));
            if (Figure.HitFlash != null)
                Figure.HitFlash.Bind(health, Figure.Sprite);
            if (Figure.HitShake != null)
                Figure.HitShake.Bind(health);

            StandAt(fieldExit);
        }

        private void SyncEncounter(EncounterSimulation encounter, bool sent)
        {
            if (encounter == _bound)
                return;

            _bound = encounter;
            if (encounter == null)
                StandAt(fieldExit);
            else if (sent)
                _awaitingLocation = true;
            else
                _mode = Mode.Following;
        }

        private void Stand()
        {
            if (_standAt == null)
                return;

            var at = LocalCentre(_standAt);
            Figure.Place(at, ArenaOf(at), Figure.FacingSign);
        }

        private void Walk()
        {
            if (_target == null)
            {
                StandAt(_standAt);
                return;
            }

            var goal = LocalCentre(_target);
            _travel.Retarget(new Coordinate(goal));

            var speed = SimulationService.Instance.HeroWalkingSpeed * ArenaSpace.Projection.CanvasUnitsPerArenaUnit;
            var arrived = _travel.Advance(speed, Time.deltaTime);

            var at = new Vector2(_travel.Position.x, _travel.Position.z);
            Figure.Place(at, ArenaOf(at), ArenaLayout.FacingSign(at.x, goal.x, Figure.FacingSign, WalkFacingDeadZone));
            SortAmongSiblings();

            if (!arrived)
                return;

            if (_bound != null)
                _mode = Mode.Following;
            else
                StandAt(_target);
        }

        /// <summary>Stands at the hero's projected position, facing his Strike target (his way kept with none).</summary>
        private void Follow()
        {
            var at = _bound.HeroPosition;
            if (!Projection.TryGetNumberOrigin(at, out var canvas))
                return;

            var target = _bound.HeroTarget;
            var sign = target != null
                ? ArenaLayout.FacingSign(at.x, target.Position.x, Figure.FacingSign, facingDeadZoneUnits)
                : Figure.FacingSign;

            Figure.Place(canvas, at, sign);
            SortAmongSiblings();
        }

        // The anchor's centre in this figure's space, as the arena places its own figures.
        private Vector2 LocalCentre(RectTransform anchor)
        {
            var parent = (RectTransform)transform.parent;
            var world = anchor.TransformPoint(anchor.rect.center);
            return (Vector2)parent.InverseTransformPoint(world) - parent.rect.center;
        }

        // Where a point of the map is on the arena, for the depth sort: the selected Location's centre is the Origin.
        private Coordinate ArenaOf(Vector2 canvas)
        {
            var anchor = ArenaSpace.LocationAnchor;
            if (_bound == null || anchor == null)
                return default;

            var projection = ArenaSpace.Projection;
            var offset = canvas - LocalCentre(anchor);
            var depth = projection.Tilt > 0f ? offset.y / (projection.CanvasUnitsPerArenaUnit * projection.Tilt) : 0f;
            return _bound.Arena.Origin + new Coordinate(offset.x / projection.CanvasUnitsPerArenaUnit, depth);
        }

        // Nearer draws in front, as the arena orders its own figures: the hero takes his place among them.
        private void SortAmongSiblings()
        {
            var parent = transform.parent;
            var farther = 0;
            for (var i = parent.childCount; i-- > 0;)
            {
                var sibling = parent.GetChild(i);
                if (sibling != transform && sibling.TryGetComponent<Figure>(out var other) && other.IsPlaced
                    && ArenaProjection.DepthOrder(other.ArenaPosition, Figure.ArenaPosition) < 0)
                    farther++;
            }

            if (transform.GetSiblingIndex() != farther)
                transform.SetSiblingIndex(farther);
        }
    }
}
