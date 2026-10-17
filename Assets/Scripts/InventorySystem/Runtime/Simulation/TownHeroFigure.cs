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
    /// Pause does not stop it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownHeroFigure : MonoBehaviour, ITownHeroFigure
    {
        [SerializeField, Tooltip("Where the figure stands when it has not walked in: entering the game, a hero load, a Respawn. Empty: the Town Exit.")]
        private RectTransform waypoint;
        [SerializeField, Tooltip("The arena whose canvas scale turns the hero's walking speed (arena units) into canvas units.")]
        private EnemyArena arena;

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

            Enter(TownEntry.Play);
        }

        private void OnDisable()
        {
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

        /// <inheritdoc/>
        public void SetExit(RectTransform exit) => _exit = exit;

        private void Enter(TownEntry entry)
        {
            _stand = TownStartPoint.For(entry);
            _standAt = null;
            _target = null;
            Settle(force: true);
        }

        private void Update()
        {
            if (!_travel.IsWalking)
            {
                Settle(force: false);
                return;
            }

            var from = _travel.Position;
            var arrived = _travel.Advance(ArenaSpace.HeroCanvasSpeed, SimulationService.Instance.WalkDelta(Time.deltaTime));
            Show(from);

            if (arrived)
            {
                _standAt = _target;
                _target = null;
            }
        }

        // A standing figure keeps to its point, re-read every frame: the canvas has not laid its rects out when the
        // figure first enables, and a point that moves later takes the figure with it. Nothing while no point is known.
        private void Settle(bool force)
        {
            var point = StandPoint;
            if (point == null)
                return;

            var at = Local(point);
            if (!force && Coordinate.Distance(at, _travel.Position) < 1e-3f)
                return;

            _travel = new HeroTravel(at);
            Show(at);
        }

        /// <inheritdoc/>
        public void PlaceAtWaypoint()
        {
            _stand = TownStand.Waypoint;
            _standAt = null;
            _target = null;
            Settle(force: true);
        }

        /// <inheritdoc/>
        public void Place(RectTransform point)
        {
            _standAt = point;
            _target = null;
            Settle(force: true);
        }

        /// <inheritdoc/>
        public void TravelTo(RectTransform point)
        {
            _target = point;
            _standAt = null;
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
                Rect.localScale = new Vector3(Mathf.Sign(dx), 1f, 1f);
        }
    }
}
