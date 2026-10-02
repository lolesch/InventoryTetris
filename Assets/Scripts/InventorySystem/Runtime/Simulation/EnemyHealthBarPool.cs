using System.Collections.Generic;
using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Owns the pooled <see cref="EnemyHealthBarDisplay"/> rows in the combat panel's enemy HP
    /// bar list (issue #60) and keeps one bar per living enemy of the live Encounter (issue #94).
    ///
    /// Only the <i>binding</i> is polled — <see cref="Update"/> compares the Run's current
    /// <see cref="EncounterSimulation"/> to the one it holds, once a frame. The sim is rebuilt on
    /// every <see cref="RunState.Send"/> and <see cref="RunState.Relocate"/>, and <see cref="SimulationProvider"/> may not exist when
    /// this enables, so there is no moment to subscribe once and be done; and doing it in one
    /// place in <c>Update</c> leaves no <c>Awake</c>/<c>OnDisable</c> asymmetry to go silently
    /// dead on the second Play entry under disabled domain reload (<c>codebase-notes.md</c>).
    /// Everything else is events: <see cref="EncounterSimulation.EnemySpawned"/> takes a bar,
    /// <see cref="EncounterSimulation.EnemyDefeated"/> gives it back, and each bar follows its own
    /// enemy's health.
    ///
    /// A clear is safe by construction (it needs an empty enemy list, so every bar was already
    /// released by <c>EnemyDefeated</c>). A hero death or an auto-Recall leaves live enemies with
    /// nothing firing for them — the Run ending nulls the Encounter, and that release-all is what
    /// empties the list.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBarPool : MonoBehaviour
    {
        [SerializeField] private EnemyHealthBarDisplay prefab;
        [SerializeField] private Transform container;

        private readonly Dictionary<Enemy, EnemyHealthBarDisplay> _bars = new();
        private PrefabPool<EnemyHealthBarDisplay> _pool;
        private EncounterSimulation _bound;

        // Lazily built rather than in Awake, which does not run again on the second Play entry
        // under disabled domain reload — the scene object survives, only OnEnable/Update repeat.
        private PrefabPool<EnemyHealthBarDisplay> Pool =>
            _pool ??= new PrefabPool<EnemyHealthBarDisplay>(prefab, container != null ? container : transform);

        private void Update()
        {
            var provider = SimulationProvider.Instance;
            var encounter = provider != null ? provider.Run.Encounter : null;
            if (encounter == _bound)
                return;

            Unbind();
            if (encounter != null)
                Bind(encounter);
        }

        // Update never runs while disabled, so a disabled pool would hold a subscription no one
        // can retire. Letting go here means the first Update after re-enabling binds afresh.
        private void OnDisable() => Unbind();

        private void Bind(EncounterSimulation encounter)
        {
            _bound = encounter;
            _bound.EnemySpawned += OnEnemySpawned;
            _bound.EnemyDefeated += OnEnemyDefeated;

            // The first Encounter's initial batch spawns inside the sim's constructor, before this
            // could listen — and a Send can land between frames with enemies already in. Seeding
            // oldest-first leaves the newest on top, matching what the events would have built.
            for (var i = 0; i < encounter.Enemies.Count; i++)
                SpawnBar(encounter.Enemies[i]);
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
            foreach (var bar in _bars.Values)
                Release(bar);
            _bars.Clear();
        }

        private void OnEnemySpawned(Enemy enemy) => SpawnBar(enemy);

        private void OnEnemyDefeated(Enemy enemy) => RemoveBar(enemy);

        /// <summary>Activates a pooled bar for <paramref name="enemy"/> and puts it at the top of the list.</summary>
        private void SpawnBar(Enemy enemy)
        {
            if (_bars.ContainsKey(enemy))
                return;

            var bar = Pool.GetObject();
            bar.Bind(enemy);
            bar.transform.SetAsFirstSibling();
            _bars.Add(enemy, bar);
        }

        /// <summary>Releases <paramref name="enemy"/>'s bar back to the pool.</summary>
        private void RemoveBar(Enemy enemy)
        {
            if (_bars.Remove(enemy, out var bar))
                Release(bar);
        }

        // OnDisable also runs as Play mode tears the scene down, when a bar may already be
        // destroyed — releasing one would throw, and one exception pauses the Editor.
        private void Release(EnemyHealthBarDisplay bar)
        {
            if (bar == null)
                return;

            bar.Unbind();
            Pool.ReleaseObject(bar);
        }
    }
}
