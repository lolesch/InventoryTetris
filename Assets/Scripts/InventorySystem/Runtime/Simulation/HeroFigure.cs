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
    /// Death beat; a Respawn stands it up at the Field Exit. A Recall that holds the Run walks it into the portal beside
    /// it (shown only, ADR-0021) and it stays gone until the Run is live again or the Town Exit brings the Field map
    /// up over the held Run, which stands it at the Field Exit. It polls the Run's Encounter once a frame, since a
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
        [SerializeField, Tooltip("The Recall portal's rect, which the figure walks into once the Run is held. Empty: he is gone at once.")]
        private RectTransform portal;
        [SerializeField, Min(0f), Tooltip("Seconds he takes to shrink and fade once he has walked into the portal, before the map changes.")]
        private float vanishSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("Seconds the map waits after he is gone before it changes.")]
        private float holdSeconds = 0.25f;

        private enum Mode
        {
            Standing,
            Walking,
            Following,
            Fallen,
            Entering,
            Leaving,
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
        private bool _standsAtExit;
        private bool _awaitingLocation;
        private bool _arrivesByPortal;
        private Vector2? _fallenAt;
        private float _vanishFor;
        private float _visible = 1f;
        private bool _inPortal;
        private bool _emerging;
        private bool _steppingOut;
        private float _emergeFor;
        private FieldPortalMarker _portalMarker;
        private System.Action _onLeft;

        private Figure Figure => _figure ? _figure : _figure = GetComponent<Figure>();

        private IDamageNumberOrigins Projection => arena;

        private IArenaSpace ArenaSpace => arena;

        /// <inheritdoc/>
        public bool IsWalking => _mode == Mode.Walking;

        /// <summary>
        /// Whether a held Run's hero has gone into the portal: the walk is over and the figure is gone. Cleared the
        /// moment the Run is not held any more.
        /// </summary>
        public bool HasEnteredPortal => _inPortal;

        /// <summary>Whether he is on his way into the portal, walking to it or vanishing in it.</summary>
        public bool IsEnteringPortal => _mode == Mode.Entering;

        /// <summary>Whether the figure has been drawn anywhere yet.</summary>
        public bool IsPlaced => Figure.IsPlaced;

        /// <summary>Whether he has come through a portal and is not yet clear of it: waiting for it to open, growing on top of it, or walking to the fight.</summary>
        public bool IsEmerging => _emerging;

        /// <summary>Whether he is walking to the Field Exit to Return, the Run waiting for him.</summary>
        public bool IsLeaving => _mode == Mode.Leaving;

        /// <summary>Raised once when he has gone into the portal: the map changes now.</summary>
        public event System.Action PortalEntered;

        /// <summary>Raised once when he has come out of a portal and walked clear of it: the Run goes on from here.</summary>
        public event System.Action Arrived;

        /// <summary>
        /// The Field Exit, announced by its button: where the figure stands when the Field map comes up and walks out on
        /// a Return. Held as a stance, not a rect, so an exit announced after the figure first stood still takes him.
        /// </summary>
        public void SetExit(RectTransform exit) => _exit = exit;

        /// <inheritdoc/>
        public void StandAt(RectTransform anchor)
        {
            _standsAtExit = anchor != null && anchor == _exit;
            _standAt = _standsAtExit ? null : anchor;
            _target = null;
            _awaitingLocation = false;
            _mode = Mode.Standing;
        }

        // Stands at the Exit, whichever rect it is when he is drawn; none announced yet draws nothing.
        private void StandAtExit()
        {
            StandAt(null);
            _standsAtExit = true;
        }

        /// <inheritdoc/>
        public void ArriveByPortal() => _arrivesByPortal = true;

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
            var sent = sameRun && _phase == RunPhase.None && run.Phase == RunPhase.Live;
            var relocated = sameRun && _bound != null && run.Encounter != null && run.Encounter != _bound;
            var held = sameRun && _phase != RunPhase.Held && run.Phase == RunPhase.Held;
            var resumed = sameRun && _phase == RunPhase.Held && run.Phase == RunPhase.Live && !relocated;
            _run = run;
            _phase = run.Phase;
            _inPortal &= _phase == RunPhase.Held;

            // A Run that ended leaves him nothing to come out of or to leave by the Exit.
            if (_emerging && (_phase == RunPhase.None || _mode != Mode.Following))
                StopEmerging();

            if (_mode == Mode.Leaving && _phase != RunPhase.Live)
            {
                _mode = Mode.Standing;
                _onLeft = null;
            }

            // A Run that is not held any more (a Relocate while he walks to the portal) leaves him nothing to walk into.
            if (_mode == Mode.Entering && _phase != RunPhase.Held)
                _mode = Mode.Standing;

            // Through the Town Portal with the Run held: he comes out of it on the Field map and the Run resumes once he is clear.
            if (_arrivesByPortal && _phase == RunPhase.Held && _bound != null)
            {
                _arrivesByPortal = false;
                _inPortal = false;
                _mode = Mode.Following;
                BeginEmerging();
            }

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
            {
                // Out of a Town Portal he is at the fight at once; the walk is for a hero who set out from the Exit.
                if (_arrivesByPortal)
                {
                    _awaitingLocation = false;
                    _mode = Mode.Following;
                    BeginEmerging();
                }
                else
                    TravelTo(_anchor);

                _arrivesByPortal = false;
            }

            if (held)
                WalkIntoPortal();
            else if (resumed && _bound != null)
                _mode = Mode.Following;

            switch (_mode)
            {
                case Mode.Walking: Walk(); break;
                case Mode.Leaving: Leave(); break;
                case Mode.Following when _emerging && _bound != null: StepOut(); break;
                case Mode.Following: Follow(); break;
                case Mode.Entering: Enter(); break;
                case Mode.Fallen: break;
                default: Stand(); break;
            }

            // When the Death beat ends the fallen figure is gone: the Corpse marker is what lies there now.
            var gone = _mode == Mode.Fallen && !SimulationService.Instance.DeathBeatPlaying;

            // Shrunk and faded together while he vanishes into the portal and grown back as he steps out of one
            // (StepOut drives that); whole again once he is neither.
            if (!_emerging && _mode != Mode.Entering && !_inPortal)
                _visible = 1f;

            transform.localScale = Vector3.one * _visible;
            Figure.SetAlpha(gone || _inPortal ? 0f : (map != null ? map.alpha : 1f) * _visible);

            // The Run waits only while he is on a trip; a hold nothing here owns is let go.
            var simulation = SimulationService.Instance;
            if (simulation.IsHeroInTransit && !_emerging && !_arrivesByPortal && _mode != Mode.Leaving)
                simulation.SetHeroInTransit(false);
        }

        private FieldFacePanel _face;

        private void OnEnable()
        {
            _face = map ? map.GetComponent<FieldFacePanel>() : null;
            if (_face)
                _face.Appearing += OnFieldMapAppearing;
        }

        // The Town Exit brings the Field map up over a held Run: he steps out of the portal at the Field Exit and the
        // portal stays open. A resume lands live, so it is not this.
        private void OnFieldMapAppearing()
        {
            // On his way out of the Town Portal the Run resumes at once: he stays gone until he steps out of it.
            if (!_inPortal || _arrivesByPortal || SimulationService.Instance.Run.Phase != RunPhase.Held)
                return;

            _inPortal = false;
            _visible = 1f;
            StandAtExit();
        }

        // Update never runs while disabled, so a disabled map would hold a figure no one can retire.
        private void OnDisable()
        {
            if (_face)
                _face.Appearing -= OnFieldMapAppearing;

            _face = null;

            if (_hero != null)
                Figure.Unbind();

            _hero = null;
            _bound = null;
            _run = null;
            _phase = default;
            _inPortal = false;
            _emerging = false;
            _onLeft = null;
            _arrivesByPortal = false;
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
                Figure.HitFlash.Bind(hero, Figure.Sprite);
            if (Figure.HitShake != null)
                Figure.HitShake.Bind(health);

            if (_exit == null)
                Debug.LogWarning($"{name}: no Field Exit announced - wire the ToTownButton's Hero Figure to this figure, or he is not drawn until one is.", this);

            StandAtExit();
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
                    StandAtExit();
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

        /// <summary>
        /// The Run is held: he walks from where the fight has him into the portal beside him, at his walking speed, and
        /// vanishes in it. The map changes when <see cref="PortalEntered"/> is raised.
        /// </summary>
        public void WalkIntoPortal()
        {
            if (portal == null || !Figure.IsPlaced)
            {
                FinishEntering();
                return;
            }

            _travel.Place(new Coordinate(((RectTransform)transform).anchoredPosition));
            _travel.Retarget(new Coordinate(LocalCentre(portal)));
            _vanishFor = 0f;
            _target = null;
            _mode = Mode.Entering;
        }

        // The portal beside him, which he waits for while it grows; none, or one that is not up, is nothing to wait for.
        private bool PortalIsGrowing
        {
            get
            {
                if (!_portalMarker && portal)
                    portal.TryGetComponent(out _portalMarker);

                return _portalMarker && _portalMarker.isActiveAndEnabled && !_portalMarker.IsOpen;
            }
        }

        // Going in reversed: he is gone until the portal beside the fight has grown, grows from nothing on top of it, and
        // walks the few steps to where the fight has him.
        private void BeginEmerging()
        {
            _emerging = true;
            _emergeFor = 0f;
            _visible = 0f;
            _steppingOut = false;

            // A live Run waits for him; a held one is not moving anyway.
            SimulationService.Instance.SetHeroInTransit(true);
        }

        // He is clear of the portal: whatever waited for him goes on.
        private void StopEmerging()
        {
            if (!_emerging)
                return;

            _emerging = false;
            Arrived?.Invoke();
        }

        private void StepOut()
        {
            // A held Run's fight may not be drawn yet; its place on the map is still known, and he walks to it.
            var known = TryFightCanvas(out var fight);

            if (portal == null)
            {
                // Nothing to come out of: he grows where the fight has him.
                if (known)
                    Follow();

                GrowOut();
                return;
            }

            if (!_steppingOut)
            {
                if (PortalIsGrowing)
                {
                    _visible = 0f;
                    return;
                }

                var from = LocalCentre(portal);
                var facing = known ? ArenaLayout.FacingSign(from.x, fight.x, Figure.FacingSign, WalkFacingDeadZone) : Figure.FacingSign;
                Figure.Place(from, ArenaOf(from), facing);
                SortAmongSiblings();
                GrowOut();

                if (_emergeFor >= vanishSeconds)
                {
                    _steppingOut = true;
                    _travel.Place(new Coordinate(from));
                }

                return;
            }

            _visible = 1f;

            // Nowhere to walk to: he is out and that is all.
            if (!known)
            {
                StopEmerging();
                return;
            }

            _travel.Retarget(new Coordinate(fight));
            var arrived = _travel.Advance(ArenaSpace.HeroCanvasSpeed, SimulationService.Instance.WalkDelta(Time.deltaTime));

            var here = new Vector2(_travel.Position.x, _travel.Position.z);
            Figure.Place(here, ArenaOf(here), ArenaLayout.FacingSign(here.x, fight.x, Figure.FacingSign, WalkFacingDeadZone));
            SortAmongSiblings();

            if (arrived)
                StopEmerging();
        }

        // Where the fight has him on this map: the arena's own projection when it is drawing him, else the same place
        // worked out from the Location's anchor, as the portal markers place themselves.
        private bool TryFightCanvas(out Vector2 canvas)
        {
            if (Projection.TryGetNumberOrigin(_bound.HeroPosition, out canvas))
                return true;

            if (_anchor == null)
                return false;

            var offset = ArenaLayout.CorpseOffset(_bound.HeroPosition, _bound.Arena.Origin, ArenaSpace.Projection);
            var parent = (RectTransform)transform.parent;
            canvas = LocalCentre(_anchor) + (Vector2)parent.InverseTransformVector(_anchor.TransformVector(offset));
            return true;
        }

        // Grows from nothing over the vanish's beat, the reverse of going in; with no portal that is all of it.
        private void GrowOut()
        {
            _emergeFor += Time.deltaTime;
            _visible = 1f - PortalVanish.Remaining(_emergeFor, vanishSeconds);

            if (portal == null && _emergeFor >= vanishSeconds)
                StopEmerging();
        }

        /// <summary>
        /// A click on the Field Exit: with a Run in the Field he walks to the Exit at his own pace, the Run waiting for
        /// him, and <paramref name="onArrived"/> runs once he stands there. Anywhere else there is no walk and it runs at once.
        /// </summary>
        public void WalkToExit(System.Action onArrived)
        {
            if (_exit == null || _bound == null || _phase != RunPhase.Live || !Figure.IsPlaced || _emerging || _mode == Mode.Entering)
            {
                onArrived();
                return;
            }

            _travel.Place(new Coordinate(((RectTransform)transform).anchoredPosition));
            _travel.Retarget(new Coordinate(LocalCentre(_exit)));
            _onLeft = onArrived;
            _target = null;
            _mode = Mode.Leaving;
            SimulationService.Instance.SetHeroInTransit(true);
        }

        private void Leave()
        {
            var goal = LocalCentre(_exit);
            _travel.Retarget(new Coordinate(goal));
            var arrived = _travel.Advance(ArenaSpace.HeroCanvasSpeed, SimulationService.Instance.WalkDelta(Time.deltaTime));

            var at = new Vector2(_travel.Position.x, _travel.Position.z);
            Figure.Place(at, ArenaOf(at), ArenaLayout.FacingSign(at.x, goal.x, Figure.FacingSign, WalkFacingDeadZone));
            SortAmongSiblings();

            if (!arrived)
                return;

            // At the Exit: the Run is his to leave now.
            _mode = Mode.Following;
            SimulationService.Instance.SetHeroInTransit(false);
            var onLeft = _onLeft;
            _onLeft = null;
            onLeft?.Invoke();
        }

        private void Enter()
        {
            if (PortalIsGrowing)
                return;

            if (_travel.IsWalking)
            {
                var goal = LocalCentre(portal);
                _travel.Retarget(new Coordinate(goal));
                _ = _travel.Advance(ArenaSpace.HeroCanvasSpeed, SimulationService.Instance.WalkDelta(Time.deltaTime));

                var at = new Vector2(_travel.Position.x, _travel.Position.z);
                Figure.Place(at, ArenaOf(at), ArenaLayout.FacingSign(at.x, goal.x, Figure.FacingSign, WalkFacingDeadZone));
                SortAmongSiblings();
                return;
            }

            _vanishFor += Time.deltaTime;
            _visible = PortalVanish.Remaining(_vanishFor, vanishSeconds);

            // Gone, the map waits a beat before it changes.
            if (_vanishFor >= vanishSeconds + holdSeconds)
                FinishEntering();
        }

        private void FinishEntering()
        {
            _mode = Mode.Standing;
            _inPortal = true;
            PortalEntered?.Invoke();
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
            StandAtExit();
        }

        private void Stand()
        {
            var anchor = _standsAtExit ? _exit : _standAt;
            if (anchor == null)
                return;

            var at = LocalCentre(anchor);
            Figure.Place(at, ArenaOf(at), Figure.FacingSign);
        }

        private void Walk()
        {
            if (_target == null)
            {
                StandAt(_standsAtExit ? _exit : _standAt);
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
