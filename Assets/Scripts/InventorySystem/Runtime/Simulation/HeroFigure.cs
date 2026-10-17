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
    /// The Field map's own hero <see cref="Figure"/>: one scene object that follows the hero while the map is up and
    /// fades with it. It stands at an anchor, walks to one (Travel, ADR-0021: shown, never simulated, so Pause leaves
    /// it walking) or follows the fight, projected by the arena (ADR-0018). A Death lays it where he fell for the
    /// Death beat; a Respawn stands it up at the Field Exit. It polls the Run's Encounter once a frame, since a
    /// phase change or a new Encounter is the only way to tell a Send or a Relocate from a hero load mid-fight, and
    /// updates after the arena so the arena has placed its own figures first.
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
        [SerializeField, Tooltip("The Field map's canvas group. The figure fades with it. Empty: always opaque.")]
        private CanvasGroup map;

        private enum Mode
        {
            Standing,
            Walking,
            Following,
            Fallen,
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
        // The Field Exit, announced by its button: where the figure stands when the Field map comes up.
        private RectTransform _exit;
        private bool _awaitingLocation;
        private Vector2? _fallenAt;

        private Figure Figure => _figure ? _figure : _figure = GetComponent<Figure>();

        private IDamageNumberOrigins Projection => arena;

        private IArenaSpace ArenaSpace => arena;

        /// <inheritdoc/>
        public bool IsWalking => _mode == Mode.Walking;

        /// <inheritdoc/>
        public void SetExit(RectTransform exit) => _exit = exit;

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

        // The arena's Location anchor, resolved once per Update for everything it drives.
        private RectTransform _anchor;

        private void Update()
        {
            _anchor = ArenaSpace.LocationAnchor;
            var run = SimulationService.Instance.Run;
            var sameRun = run == _run;
            var sent = sameRun && _phase == RunPhase.InTown && run.Phase == RunPhase.InField;
            var relocated = sameRun && _bound != null && run.Encounter != null && run.Encounter != _bound;
            _run = run;
            _phase = run.Phase;

            SyncHero();

            // A hero who is down with no fight left lies where he fell until Respawn stands him up.
            if (_hero.IsDead && run.Encounter == null)
                Fall();
            else
            {
                if (_mode == Mode.Fallen)
                    GetUp();

                SyncEncounter(run.Encounter, sent, relocated, run.LastResult is { Outcome: RunOutcome.Returned });
            }

            // A Send's walk starts once the Location's anchor can be read, as the arena waits for it too.
            if (_awaitingLocation && _anchor != null)
                TravelTo(_anchor);

            switch (_mode)
            {
                case Mode.Walking: Walk(); break;
                case Mode.Following: Follow(); break;
                case Mode.Fallen: break;
                default: Stand(); break;
            }

            // When the Death beat ends the fallen figure is gone: the Corpse marker is what lies there now.
            var gone = _mode == Mode.Fallen && !SimulationService.Instance.DeathBeatPlaying;
            Figure.SetAlpha(gone ? 0f : map != null ? map.alpha : 1f);
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

            StandAt(_exit);
        }

        private void SyncEncounter(EncounterSimulation encounter, bool sent, bool relocated, bool returned)
        {
            if (encounter == _bound)
                return;

            var wasBound = _bound != null;
            _bound = encounter;
            if (encounter == null)
            {
                // A Return walks out from where the fight last drew him; a Death or a hero load stands him at the Exit.
                if (returned && wasBound && _exit != null)
                    TravelTo(_exit);
                else
                    StandAt(_exit);
            }
            else if (sent || relocated)
            {
                // A Relocate leaves the figure drawn where the old fight had it; both walks start once the Location's anchor can be read.
                if (relocated)
                    StandAt(null);

                _awaitingLocation = true;
            }
            else
                _mode = Mode.Following;
        }

        // The arena holds the ended Encounter through the Death beat, so the hero's last position is still projectable.
        // He does not move, so he is placed when first projectable and again only if the arena's drawing moves.
        private void Fall()
        {
            if (_mode != Mode.Fallen)
            {
                _mode = Mode.Fallen;
                _target = null;
                _awaitingLocation = false;
                _fallenAt = null;
                Figure.LieDown();
            }

            if (_bound == null || !Projection.TryGetNumberOrigin(_bound.HeroPosition, out var canvas) || _fallenAt == canvas)
                return;

            Figure.Place(canvas, _bound.HeroPosition, Figure.FacingSign);
            _fallenAt = canvas;
        }

        private void GetUp()
        {
            Figure.GetUp();
            _bound = null;
            StandAt(_exit);
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

            var arrived = _travel.Advance(ArenaSpace.HeroCanvasSpeed, SimulationService.Instance.WalkDelta(Time.deltaTime));

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
            var anchor = _anchor;
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
