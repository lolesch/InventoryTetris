using Submodules.Utility.Extensions;
using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The hero drawn on the Town map and walking it (ADR-0021), in its parent's canvas space. It walks at the sim's
    /// pace through the arena's canvas scale, so a walk and a fight move alike; nothing waits for it to arrive and
    /// Pause does not stop it. A Recall that holds the Run stands it at the Waypoint, beside the Town Portal.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class TownHeroFigure : MonoBehaviour, ITownHeroFigure
    {
        [SerializeField, Tooltip("Where the figure stands when it has not walked in: entering the game, a hero load, a Respawn. Empty: the Town Exit.")]
        private RectTransform waypoint;
        [SerializeField, Tooltip("The arena whose canvas scale turns the hero's walking speed (arena units) into canvas units.")]
        private EnemyArena arena;
        [SerializeField, Tooltip("The Field map's Hero figure: when it goes into a portal, this one comes out beside the Town Portal.")]
        private HeroFigure fieldFigure;
        [SerializeField, Min(0f), Tooltip("Seconds the figure takes to shrink and fade once it has walked into the portal, before the map changes.")]
        private float vanishSeconds = 0.5f;
        [SerializeField, Min(0f), Tooltip("Seconds the map waits after the figure is gone before it changes.")]
        private float holdSeconds = 0.25f;

        // Standing at the origin until the first Place, so a walk query or TravelTo before OnEnable is safe.
        private IHeroTravel _travel = new HeroTravel(default);

        // The Town Exit, announced by its button: where the figure stands after a Return.
        private RectTransform _exit;
        // Where the figure stands while it is not walking: the point it was placed at or walked to, else the entry's.
        private RectTransform _standAt;
        private RectTransform _target;
        private TownStand _stand = TownStand.Waypoint;
        private RectTransform _rect;
        private RectTransform _map;
        private CanvasGroup _group;
        private RunPhasePanel _face;

        // Going into a portal: walking to it, then vanishing; the command waits until he is gone.
        private System.Action _onGone;

        // Going to a point to do something there (the Exit): the command waits until he stands at it.
        private System.Action _onArrived;
        private bool _vanishing;
        private float _vanishFor;
        private float _look = 1f;
        private float _facing = 1f;

        // A portal brought him in beside the Town Portal; any other way onto the Town map with a held Run is a walk back.
        private bool _arrivedByPortal;

        // Out of the portal: gone until the Town Portal has grown, then growing back.
        private bool _emerging;
        private float _emergeFor;
        private TownPortalMarker _portalMarker;

        // Where he stands beside the Town Portal once he has come out of it, in the map's space; none otherwise.
        private Vector2? _besideStand;

        private CanvasGroup Group => _group ? _group : _group = GetComponent<CanvasGroup>();

        // The rect the figure sits under: positions and the walk are in its local space (canvas units).
        private RectTransform Map => _map ? _map : _map = (RectTransform)transform.parent;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        private IArenaSpace ArenaSpace => arena;

        /// <inheritdoc/>
        public bool IsWalking => _travel.IsWalking;

        /// <inheritdoc/>
        public Vector2 Position => Rect.localPosition;

        // Subscribed here, not in Awake: with the scene reload off, only OnEnable runs again on the next Play entry.
        private void OnEnable()
        {
            if (Session.TrySubscribeHeroLoaded(OnHeroLoaded))
            {
                SimulationService.Instance.RunSettled += OnRunSettled;
                SimulationService.Instance.Respawned += OnRespawned;
            }

            if (fieldFigure)
                fieldFigure.PortalEntered += OnPortalEntered;

            _face = GetComponentInParent<RunPhasePanel>();
            if (_face)
                _face.Appearing += OnTownMapAppearing;

            Enter(TownEntry.Play);
        }

        private void OnDisable()
        {
            if (fieldFigure)
                fieldFigure.PortalEntered -= OnPortalEntered;

            if (_face)
                _face.Appearing -= OnTownMapAppearing;

            _face = null;
            Session.UnsubscribeHeroLoaded(OnHeroLoaded);
            if (ServiceLocator.IsArmed)
            {
                SimulationService.Instance.RunSettled -= OnRunSettled;
                SimulationService.Instance.Respawned -= OnRespawned;
            }
        }

        private void OnHeroLoaded() => Enter(TownEntry.Load);

        private void OnRespawned() => Enter(TownEntry.Respawn);

        // A Respawn lands at the Waypoint, wherever the hero fell. A Return brings him in through the Exit;
        // a Death leaves him where he fell until then.
        private void OnRunSettled(RunResult result)
        {
            if (result.Outcome == RunOutcome.Returned)
                Enter(TownEntry.Return);
        }

        private RectTransform Waypoint => waypoint ? waypoint : _exit;

        private RectTransform StandPoint => _standAt ? _standAt : _stand == TownStand.TownExit ? _exit : Waypoint;

        /// <summary>
        /// The Town Exit, announced by its button: where the figure stands after a Return. Held as a stance, not a rect,
        /// so an exit announced after the figure stood, or announced anew, still takes him with it.
        /// </summary>
        public void SetExit(RectTransform exit) => _exit = exit;

        // The Field figure went into a portal: he comes out beside the Town Portal.
        private void OnPortalEntered()
        {
            _arrivedByPortal = true;
            Enter(TownEntry.Recall);

            // He is not there until the portal has grown; then he grows back, the way he went in.
            _emerging = true;
            _emergeFor = 0f;
            Look(0f);
        }

        // The Town Portal, which he waits for while it grows; none, or one that is not up, is nothing to wait for.
        private bool PortalIsGrowing
        {
            get
            {
                if (!_portalMarker && _face)
                    _portalMarker = _face.GetComponentInChildren<TownPortalMarker>(true);

                return _portalMarker && _portalMarker.isActiveAndEnabled && !_portalMarker.IsOpen;
            }
        }

        // Gone until the portal has grown; then he grows on top of it and walks the few steps to where he stands.
        private void Emerge()
        {
            if (PortalIsGrowing)
            {
                Look(0f);
                return;
            }

            var at = _portalMarker ? Local((RectTransform)_portalMarker.transform) : _travel.Position;
            if (_portalMarker)
            {
                _travel = new HeroTravel(at);
                Show(at);
            }

            _emergeFor += Time.deltaTime;
            Look(1f - PortalVanish.Remaining(_emergeFor, vanishSeconds));

            if (_emergeFor < vanishSeconds)
                return;

            _emerging = false;
            if (_portalMarker)
            {
                // He stands where the portal's Beside says, and stays there: a stance of its own, not one of the marked points.
                _besideStand = new Vector2(at.x, at.z) + _portalMarker.Beside;
                _standAt = null;
                _target = null;
                _travel.Retarget(new Coordinate(_besideStand.Value));
            }
            else if (StandPoint != null)
                TravelTo(StandPoint);
        }

        // The Town map came up over a held Run without a portal: he came back by the Field Exit and stands at the Town
        // Exit, not where he left off.
        private void OnTownMapAppearing()
        {
            if (!_arrivedByPortal && SimulationService.Instance.Run.Phase == RunPhase.Held)
                Enter(TownEntry.Return);

            _arrivedByPortal = false;
        }

        private void Enter(TownEntry entry)
        {
            _stand = TownStartPoint.For(entry);
            _standAt = null;
            _target = null;
            _besideStand = null;
            _onGone = null;
            _onArrived = null;
            _vanishing = false;
            _emerging = false;
            Look(1f);
            Settle(force: true);
        }

        // Scale and opacity together; his facing is kept.
        private void Look(float factor)
        {
            _look = factor;
            Rect.localScale = new Vector3(_facing * factor, factor, 1f);
            Group.alpha = factor;
        }

        // Stands at <paramref name="point"/> from now on: the Exit as the Exit stance, any other point as itself.
        private void StandAtPoint(RectTransform point)
        {
            var atExit = point == _exit;
            _stand = atExit ? TownStand.TownExit : _stand;
            _standAt = atExit ? null : point;
            _target = null;
            _besideStand = null;
        }

        private void Update()
        {
            if (_emerging)
            {
                Emerge();

                // Standing at the portal, not at the point he walks to next.
                if (_emerging)
                    return;
            }

            if (_vanishing)
            {
                Vanish();
                return;
            }

            if (!_travel.IsWalking)
            {
                Settle(force: false);
                return;
            }

            var from = _travel.Position;
            var arrived = _travel.Advance(ArenaSpace.HeroCanvasSpeed, SimulationService.Instance.WalkDelta(Time.deltaTime));
            Show(from);

            if (!arrived)
                return;

            // Arriving at his Beside is no marked point to stand at.
            if (!_besideStand.HasValue)
                StandAtPoint(_target);

            _vanishing = _onGone != null;

            var onArrived = _onArrived;
            _onArrived = null;
            onArrived?.Invoke();
        }

        private void Vanish()
        {
            _vanishFor += Time.deltaTime;
            var left = PortalVanish.Remaining(_vanishFor, vanishSeconds);
            Look(left);

            // Gone, the map waits a beat before it changes.
            if (_vanishFor < vanishSeconds + holdSeconds)
                return;

            // Gone: he stays gone until the next way into Town puts him back.
            _vanishing = false;
            var onGone = _onGone;
            _onGone = null;
            onGone();
        }

        /// <inheritdoc/>
        public void WalkIntoPortal(RectTransform portal, System.Action onGone)
        {
            TravelTo(portal);
            _onGone = onGone;
            _vanishFor = 0f;

            // Already at the portal: nothing walks, so he starts to vanish at once.
            _vanishing = !_travel.IsWalking;
        }

        /// <inheritdoc/>
        public void WalkThen(RectTransform point, System.Action onArrived)
        {
            TravelTo(point);

            // Already there: TravelTo stood him at it and nothing walks.
            if (!_travel.IsWalking)
            {
                onArrived();
                return;
            }

            _onArrived = onArrived;
        }

        // A standing figure keeps to its point, re-read every frame: the canvas has not laid its rects out when the
        // figure first enables, and a point that moves later takes the figure with it. Nothing while no point is known.
        private void Settle(bool force)
        {
            var point = StandPoint;
            if (point == null && !_besideStand.HasValue)
                return;

            var at = _besideStand.HasValue ? new Coordinate(_besideStand.Value) : Local(point);
            if (!force && Coordinate.Distance(at, _travel.Position) < 1e-3f)
                return;

            _travel = new HeroTravel(at);
            Show(at);
        }

        /// <inheritdoc/>
        public void PlaceAtWaypoint() => Enter(TownEntry.Play);

        /// <inheritdoc/>
        public void Place(RectTransform point)
        {
            StandAtPoint(point);
            Settle(force: true);
        }

        /// <inheritdoc/>
        public void TravelTo(RectTransform point)
        {
            // A walk to anywhere else turns him from a portal or an Exit he was heading for.
            _onGone = null;
            _onArrived = null;
            _target = point;
            _standAt = null;
            _besideStand = null;
            _travel.Retarget(Local(point));

            // Already there: nothing walks, so it stands at the point it was sent to.
            if (!_travel.IsWalking)
                Place(point);
        }

        // The point's centre in the space the figure walks in.
        private Coordinate Local(RectTransform point)
        {
            var world = point.TransformPoint(point.rect.center);
            return new Coordinate((Vector2)Map.InverseTransformPoint(world));
        }

        // Stands the figure at the walk's position, turned the way it just moved (its way kept when it stood).
        private void Show(Coordinate from)
        {
            var at = _travel.Position;
            Rect.localPosition = new Vector3(at.x, at.z, 0f);

            var dx = at.x - from.x;
            if (Mathf.Abs(dx) > 1e-4f)
            {
                _facing = Mathf.Sign(dx);
                Look(_look);
            }
        }
    }
}
