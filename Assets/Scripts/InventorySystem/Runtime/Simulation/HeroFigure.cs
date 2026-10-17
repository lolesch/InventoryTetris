using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Field map's own hero <see cref="Figure"/> (issue #316, epic #344): one scene object, never pooled, owned by
    /// the map rather than bound by <see cref="EnemyArena"/>. While an Encounter runs it stands where the simulation
    /// has the hero (ADR-0018), projected by the arena that draws the enemies, so the two share one picture.
    ///
    /// Like the arena, only the binding is polled: the Run's current Encounter against the one held, once a frame.
    /// It updates after the arena in the same frame, so the arena has placed its own figures and settled its centre first.
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Figure))]
    public sealed class HeroFigure : MonoBehaviour
    {
        [SerializeField, Tooltip("Projects the hero's arena position onto this map. Must share this figure's parent.")]
        private EnemyArena arena;
        [SerializeField, Min(0f), Tooltip("Arena units of horizontal offset from the hero's target under which the figure keeps the way it faces.")]
        private float facingDeadZoneUnits = 0.75f;

        private Figure _figure;
        private EncounterSimulation _bound;

        private Figure Figure => _figure ? _figure : _figure = GetComponent<Figure>();

        private IDamageNumberOrigins Projection => arena;

        private void Update()
        {
            var encounter = SimulationService.Instance.Run.Encounter;
            if (encounter != _bound)
            {
                Release();
                if (encounter != null)
                    Bind(encounter);
            }

            if (_bound != null)
                Place();
        }

        // Update never runs while disabled, so a disabled map would hold a figure no one can retire.
        private void OnDisable()
        {
            if (_bound != null)
                Release();
        }

        private void Bind(EncounterSimulation encounter)
        {
            _bound = encounter;

            var hero = Session.Instance.Hero;
            var health = hero.GetResource(StatName.Health);
            Figure.Bind(health, "Hero", hero.GetResource(StatName.Shield));
            if (Figure.HitFlash != null)
                Figure.HitFlash.Bind(health, Figure.Sprite);
            if (Figure.HitShake != null)
                Figure.HitShake.Bind(health);
        }

        // No Run, no hero on the map: the figure goes hidden, back at rest, facing right.
        private void Release()
        {
            _bound = null;
            Figure.Unbind();
        }

        /// <summary>Stands at the hero's projected position, facing his Strike target (his way kept with none).</summary>
        private void Place()
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
