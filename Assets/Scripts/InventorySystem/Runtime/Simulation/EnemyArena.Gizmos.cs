using System.Collections.Generic;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Debug gizmos for the arena: what the rules see, drawn through the same projection as the figures, so the tilt
    /// squashes a Disk into an ellipse and a figure that looks inside the shape but is not shows up. Play Mode only,
    /// and only where Gizmos are on (the Scene view; the Game view when its Gizmos toggle is on).
    /// </summary>
    public sealed partial class EnemyArena
    {
        private const int GizmoArcSegments = 48;
        private const float GizmoMarkerRadius = 0.15f; // arena units

        // The latest Cast fired, kept on screen until _castHeldUntil (real time) so it can be read.
        private int _castsSeen;
        private float _castHeldUntil;
        private Coordinate _castFrom;
        private Coordinate _castAt;

        // Each Strike that landed, until it is dropped from view.
        private readonly struct StrikeMark
        {
            public readonly Coordinate At;
            public readonly float Until;
            public readonly bool ByHero;

            public StrikeMark(Coordinate at, float until, bool byHero)
            {
                At = at;
                Until = until;
                ByHero = byHero;
            }
        }

        private readonly List<StrikeMark> _strikeMarks = new();

        // The hero's Strike is marked where it landed on the enemy; an enemy's where the enemy stood to strike.
        private void MarkStrikeForGizmos(HitEvent hit)
        {
            if (!drawGizmos || hit.DamageType != DamageType.PhysicalDamage)
                return;

            var byHero = hit.Target is Enemy;
            var at = byHero ? hit.TargetPosition : hit.Dealer is Enemy striker ? striker.Position : hit.TargetPosition;
            _strikeMarks.Add(new StrikeMark(at, Time.unscaledTime + gizmoSeconds, byHero));
        }

        // Update, not the draw call: a Cast fired while no view repainted would otherwise go unseen.
        private void TrackCastForGizmos()
        {
            if (_bound == null || !drawGizmos)
            {
                _castsSeen = 0;
                _castHeldUntil = 0f;
                _strikeMarks.Clear();
                return;
            }

            _strikeMarks.RemoveAll(mark => mark.Until <= Time.unscaledTime);

            if (_bound.CastsFired == _castsSeen)
                return;

            _castsSeen = _bound.CastsFired;
            _castFrom = _bound.LastCastFrom;
            _castAt = _bound.LastCastAt;
            _castHeldUntil = Time.unscaledTime + gizmoSeconds;
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos || !Application.isPlaying || _bound == null || !_hasCenter)
                return;

            var hero = _bound.HeroPosition;
            var cast = _bound.Cast;

            DrawRing(hero, new Coordinate(1f, 0f), _bound.CastRange, 0f, 360f, new Color(0.7f, 0.3f, 1f, 0.6f));
            DrawRing(hero, new Coordinate(1f, 0f), _bound.HeroStrikeRange, 0f, 360f, new Color(1f, 0.9f, 0.2f, 0.6f));

            var aim = _bound.CastAim;
            var shape = cast.Shape.Scaled(cast.Size);

            // What a Cast fired now would cover, faint; the one just fired, bright.
            if (aim != null)
                DrawShape(shape, cast.Anchor == AreaAnchor.Target ? aim.Position : hero, aim.Position - hero, new Color(0.7f, 0.3f, 1f, 0.35f));
            if (Time.unscaledTime < _castHeldUntil)
                DrawShape(shape, cast.Anchor == AreaAnchor.Target ? _castAt : _castFrom, _castAt - _castFrom, new Color(0.7f, 0.3f, 1f, 1f));

            var unit = arenaScale * Root.lossyScale.x;
            foreach (var mark in _strikeMarks)
            {
                Gizmos.color = mark.ByHero ? Color.yellow : new Color(1f, 0.4f, 0.2f);
                DrawCross(mark.At, unit * GizmoMarkerRadius * 2f);
            }

            foreach (var enemy in _bound.Enemies)
            {
                if (enemy.IsDown)
                    continue;

                var inside = aim != null && shape.Contains(enemy.Position, cast.Anchor, hero, aim.Position);
                Gizmos.color = inside ? Color.green : Color.red;
                DrawMarker(enemy.Position, unit * GizmoMarkerRadius);

                if (enemy == aim)
                    Gizmos.DrawWireSphere(GizmoPoint(enemy.Position), unit * GizmoMarkerRadius * 2f);
            }

            Gizmos.color = Color.white;
            DrawMarker(hero, unit * GizmoMarkerRadius);
        }

        // The arena position where the figures are drawn, in world space.
        private Vector3 GizmoPoint(Coordinate at) =>
            Root.TransformPoint(Root.rect.center + _center + _projection.ToCanvas(at, _bound.Arena.Origin));

        private void DrawMarker(Coordinate at, float size)
        {
            var point = GizmoPoint(at);
            Gizmos.DrawLine(point + Vector3.left * size, point + Vector3.right * size);
            Gizmos.DrawLine(point + Vector3.down * size, point + Vector3.up * size);
        }

        // An X, so a Strike's mark is not mistaken for a fighter's + marker.
        private void DrawCross(Coordinate at, float size)
        {
            var point = GizmoPoint(at);
            Gizmos.DrawLine(point + new Vector3(-size, -size), point + new Vector3(size, size));
            Gizmos.DrawLine(point + new Vector3(-size, size), point + new Vector3(size, -size));
        }

        // The shape as the rules place it: on `origin`, pointing along `facing` (east when it has no length).
        private void DrawShape(AreaShape shape, Coordinate origin, Coordinate facing, Color color)
        {
            var direction = Coordinate.SqrMagnitude(facing) > 0f ? Coordinate.Normalize(facing) : new Coordinate(1f, 0f);

            if (shape.Kind == AreaShapeKind.Rectangle)
            {
                var side = new Coordinate(-direction.z, direction.x);
                var near = -shape.PivotAlong * shape.Length;
                var far = (1f - shape.PivotAlong) * shape.Length;
                var right = -shape.PivotAcross * shape.Width;
                var left = (1f - shape.PivotAcross) * shape.Width;

                var a = origin + direction * near + side * right;
                var b = origin + direction * far + side * right;
                var c = origin + direction * far + side * left;
                var d = origin + direction * near + side * left;
                DrawPolyline(color, a, b, c, d, a);
                return;
            }

            var half = shape.Kind == AreaShapeKind.Sector ? shape.Angle * 0.5f : 180f;
            DrawRing(origin, direction, shape.Radius, -half, half, color);
            if (shape.InnerRadius > 0f)
                DrawRing(origin, direction, shape.InnerRadius, -half, half, color);

            if (shape.Kind != AreaShapeKind.Sector || shape.Angle >= 360f)
                return;

            foreach (var edge in new[] { -half, half })
                DrawPolyline(color,
                    origin + Coordinate.Rotate(direction, edge) * shape.InnerRadius,
                    origin + Coordinate.Rotate(direction, edge) * shape.Radius);
        }

        // An arc of `radius` around `center`, from `from` to `to` degrees off `direction`.
        private void DrawRing(Coordinate center, Coordinate direction, float radius, float from, float to, Color color)
        {
            Gizmos.color = color;
            var previous = GizmoPoint(center + Coordinate.Rotate(direction, from) * radius);
            for (var i = 1; i <= GizmoArcSegments; i++)
            {
                var next = GizmoPoint(center + Coordinate.Rotate(direction, Mathf.Lerp(from, to, i / (float)GizmoArcSegments)) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }

        private void DrawPolyline(Color color, params Coordinate[] points)
        {
            Gizmos.color = color;
            for (var i = 1; i < points.Length; i++)
                Gizmos.DrawLine(GizmoPoint(points[i - 1]), GizmoPoint(points[i]));
        }
    }
}
