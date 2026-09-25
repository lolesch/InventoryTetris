using Submodules.Utility.Tools;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Owns the pooled <see cref="EnemyHealthBarDisplay"/> rows in the combat panel's enemy HP
    /// bar list (issue #60). <see cref="SpawnBar"/>/<see cref="RemoveBar"/> are placeholders —
    /// nothing calls them yet, since wiring to <c>EncounterSimulation</c> per-enemy events is a
    /// separate ticket.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBarPool : MonoBehaviour
    {
        [SerializeField] private EnemyHealthBarDisplay prefab;
        [SerializeField] private Transform container;

        private PrefabPool<EnemyHealthBarDisplay> _pool;

        private void Awake() =>
            _pool = new PrefabPool<EnemyHealthBarDisplay>(prefab, container != null ? container : transform);

        /// <summary>Activates a pooled bar, refreshes it, and moves it to the top of the list.</summary>
        public EnemyHealthBarDisplay SpawnBar(string label, float hpFraction, float current, float max)
        {
            var bar = _pool.GetObject();
            bar.Refresh(label, hpFraction, current, max);
            bar.transform.SetAsFirstSibling();
            return bar;
        }

        /// <summary>Releases a spawned bar back to the pool.</summary>
        public void RemoveBar(EnemyHealthBarDisplay bar) => _pool.ReleaseObject(bar);
    }
}
