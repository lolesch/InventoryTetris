using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The hero drawn on the Town map and walking it (ADR-0021, epic #344), under the face that fades with that map.
    /// It is not the Field map's <see cref="HeroFigure"/>, which binds the Encounter hero. It owns a
    /// <see cref="HeroTravel"/> in its parent's local canvas space, advances it each frame at the hero's walking
    /// speed (the sim's pace through the arena's canvas scale, so a walk and a fight move alike), and nothing waits
    /// for it to arrive. Pause freezes the fight, not this walk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TownHeroFigure : MonoBehaviour, ITownHeroFigure
    {
        [SerializeField, Tooltip("Where the figure stands when it has not walked in: entering the game, a hero load, a Respawn. Its own point; it sits on the Exit today.")]
        private RectTransform waypoint;
        [SerializeField, Tooltip("The arena whose canvas scale turns the hero's walking speed (arena units) into canvas units.")]
        private EnemyArena arena;

        private IHeroTravel _travel;
        private RectTransform _rect;
        private RectTransform _map;

        // The rect the figure sits under: positions and the walk are in its local space (canvas units).
        private RectTransform Map => _map ? _map : _map = (RectTransform)transform.parent;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        /// <inheritdoc/>
        public bool IsWalking => _travel.IsWalking;

        /// <inheritdoc/>
        public Vector2 Position => Rect.localPosition;

        // Subscribed here, not in Awake: with the scene reload off, only OnEnable runs again on the next Play entry.
        private void OnEnable()
        {
            _ = Session.TrySubscribeHeroLoaded(PlaceAtWaypoint);

            PlaceAtWaypoint();
        }

        private void OnDisable() => Session.UnsubscribeHeroLoaded(PlaceAtWaypoint);

        private void Update()
        {
            if (!_travel.IsWalking)
                return;

            var from = _travel.Position;
            var speed = SimulationService.Instance.HeroWalkingSpeed * arena.CanvasUnitsPerArenaUnit;
            _ = _travel.Advance(speed, Time.deltaTime);
            Show(from);
        }

        /// <inheritdoc/>
        public void PlaceAtWaypoint()
        {
            var at = Local(waypoint);
            _travel = new HeroTravel(at);
            Show(at);
        }

        /// <inheritdoc/>
        public void TravelTo(RectTransform point) => _travel.Retarget(Local(point));

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
