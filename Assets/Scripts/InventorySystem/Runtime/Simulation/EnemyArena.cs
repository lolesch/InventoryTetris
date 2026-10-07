using System.Collections.Generic;
using Submodules.Utility.Tools;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Owns the pooled <see cref="EnemyView"/> figures standing on an ellipse around the Hero icon and
    /// keeps one per living enemy of the live Encounter (issues #94, #176). It replaces the combat
    /// panel's enemy HP bar list: the pooling and the binding are what <c>EnemyHealthBarPool</c> had.
    ///
    /// Only the <i>binding</i> is polled - <see cref="Update"/> compares the Run's current
    /// <see cref="EncounterSimulation"/> to the one it holds, once a frame. The sim is rebuilt on
    /// every <see cref="RunState.Send"/> and <see cref="RunState.Relocate"/>, and a hero load replaces the Run
    /// itself, so there is no moment to subscribe once and be done; and doing it in one
    /// place in <c>Update</c> leaves no <c>Awake</c>/<c>OnDisable</c> asymmetry to go silently
    /// dead on the second Play entry under disabled domain reload (<c>codebase-notes.md</c>).
    /// Everything else is events: <see cref="EncounterSimulation.EnemySpawned"/> takes a view,
    /// <see cref="EncounterSimulation.EnemyDefeated"/> gives it back, and each view follows its own
    /// enemy's health.
    ///
    /// A clear is safe by construction (it needs an empty enemy list, so every view was already
    /// released by <c>EnemyDefeated</c>). A hero death or an auto-Recall leaves live enemies with
    /// nothing firing for them - the Run ending nulls the Encounter, and that release-all is what
    /// empties the arena.
    ///
    /// The anchor is the active Location's Hero icon (its <see cref="ToggleCheckmark"/> image), resolved
    /// every frame from the Locations' <see cref="ToggleGroup"/>: it moves on a Relocate and goes null once
    /// the group resets, and whether the group's <c>ActiveMember</c> is already set when the Run is sent is
    /// not guaranteed, so nothing is resolved at bind time. A null anchor holds the views where they are
    /// and shows nothing new. Positions are in the arena root's space, so this object must sit outside any
    /// face that fades or clips (the minimap's InFields).
    ///
    /// The view's randomness (the slot jitter) is <see cref="Random"/>, never the sim's roll source:
    /// drawing from that would shift every seeded outcome.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyArena : MonoBehaviour
    {
        [SerializeField] private EnemyView prefab;
        [SerializeField, Tooltip("Where the views live. Empty: this object's own RectTransform.")]
        private RectTransform container;
        [SerializeField, Tooltip("The Locations' toggle group; its active member's Hero icon is the anchor.")]
        private ToggleGroup locations;
        [SerializeField] private EnemyVisuals visuals;
        [SerializeField, Min(0f), Tooltip("Canvas units of horizontal offset under which a figure keeps the way it faces.")]
        private float facingDeadZone = 12f;
        [SerializeField, Range(0f, 45f), Tooltip("Degrees a new figure may sit off the middle of the widest gap.")]
        private float slotJitterDegrees = 12f;
        [SerializeField, Min(0f), Tooltip("Canvas units beyond its ring a new figure appears at, before it walks in.")]
        private float spawnMargin = 100f;

        private readonly Dictionary<Enemy, EnemyView> _views = new();
        private readonly List<float> _angles = new();
        private readonly List<EnemyView> _ordered = new();
        private PrefabPool<EnemyView> _pool;
        private EncounterSimulation _bound;
        private AbstractToggle _anchorOwner;
        private RectTransform _anchor;

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

            Place();
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
            // oldest-first matches what the events would have built, each pick seeing the ones placed.
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
        }

        private void OnEnemySpawned(Enemy enemy) => SpawnView(enemy);

        private void OnEnemyDefeated(Enemy enemy) => RemoveView(enemy);

        /// <summary>Takes a pooled view for <paramref name="enemy"/> and gives it the emptiest slot of the ring.</summary>
        private void SpawnView(Enemy enemy)
        {
            if (_views.ContainsKey(enemy))
                return;

            _angles.Clear();
            foreach (var standing in _views.Values)
                _angles.Add(standing.SlotAngle);

            var jitter = Random.Range(-slotJitterDegrees, slotJitterDegrees) * Mathf.Deg2Rad;
            var angle = ArenaLayout.PickSlotAngle(_angles, jitter);
            var entry = visuals != null ? visuals.For(enemy.Archetype) : EnemyVisuals.Fallback;

            var view = Pool.GetObject();
            view.Bind(enemy, entry, angle);
            _views.Add(enemy, view);
        }

        /// <summary>Releases <paramref name="enemy"/>'s view back to the pool.</summary>
        private void RemoveView(Enemy enemy)
        {
            if (_views.Remove(enemy, out var view))
                Release(view);
        }

        // OnDisable also runs as Play mode tears the scene down, when a view may already be
        // destroyed - releasing one would throw, and one exception pauses the Editor.
        private void Release(EnemyView view)
        {
            if (view == null)
                return;

            view.Unbind();
            Pool.ReleaseObject(view);
        }

        /// <summary>
        /// The sim's delta for this frame, computed the way <c>SimulationService.Tick</c> does: wall delta times
        /// the Hero's sim speed, and nothing while the Run is paused. Walking on it scales with the speed slider
        /// and freezes with the sim.
        /// </summary>
        private static float SimDelta()
        {
            if (SimulationService.Instance.IsPaused)
                return 0f;

            return Time.deltaTime * Mathf.Max(0f, Session.Instance.Hero.Behaviour.SimSpeed);
        }

        /// <summary>Walks every view toward its slot around the anchor as it is this frame; nothing with no anchor.</summary>
        private void Place()
        {
            if (_views.Count == 0)
                return;

            var anchor = ResolveAnchor();
            if (anchor == null)
                return;

            var root = Root;
            var world = anchor.TransformPoint(anchor.rect.center);
            var center = (Vector2)root.InverseTransformPoint(world) - root.rect.center;

            var simDelta = SimDelta();
            var moved = false;
            foreach (var view in _views.Values)
                moved |= view.PlaceAround(center, facingDeadZone, simDelta, spawnMargin);

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

        /// <summary>Lower on screen draws in front: sibling order by Y, highest first.</summary>
        private void SortByDepth()
        {
            _ordered.Clear();
            _ordered.AddRange(_views.Values);
            _ordered.Sort((a, b) => ((RectTransform)b.transform).anchoredPosition.y
                .CompareTo(((RectTransform)a.transform).anchoredPosition.y));

            for (var i = 0; i < _ordered.Count; i++)
                _ordered[i].transform.SetSiblingIndex(i);
        }
    }
}
