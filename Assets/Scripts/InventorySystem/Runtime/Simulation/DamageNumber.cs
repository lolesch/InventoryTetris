using System;
using TMPro;
using ToolSmiths.InventorySystem.Geometry;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// One rising damage figure (issue #181), pooled by <see cref="EnemyDamageNumbers"/>. It is not a child
    /// of any <see cref="EnemyView"/>: a view fades with its death and is pooled with its enemy, and the
    /// number of the killing blow has to outlive both. It lives in the arena's space, drives itself on
    /// <i>sim</i> time (faster at x8, frozen on pause) and hands itself back when its life is over or when
    /// the Encounter it was born in is gone (Recall, hero death, Relocate: the Run ended, so nothing lingers).
    /// <para>
    /// <see cref="Show"/> sets every field, so a pooled number carries nothing of its last use.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI label;

        private RectTransform _rect;
        private Vector2 _origin;
        private float _elapsed;
        private float _duration;
        private float _height;
        private float _hold;
        private EncounterSimulation _encounter;
        private Action<DamageNumber> _done;

        private RectTransform Rect => _rect ? _rect : _rect = (RectTransform)transform;

        /// <summary>Sim seconds this number has lived.</summary>
        public float Elapsed => _elapsed;

        /// <summary>
        /// Start rising from <paramref name="origin"/> (the arena's anchored space) showing
        /// <paramref name="amount"/>. <paramref name="done"/> is called once, when the life ends or the
        /// <paramref name="encounter"/> is no longer the Run's.
        /// </summary>
        public void Show(Vector2 origin, float amount, float duration, float height, float hold,
            EncounterSimulation encounter, Action<DamageNumber> done)
        {
            _origin = origin;
            _elapsed = 0f;
            _duration = duration;
            _height = height;
            _hold = hold;
            _encounter = encounter;
            _done = done;

            label.text = DamageNumberMotion.Label(amount);
            Rect.SetAsLastSibling();
            Apply();
        }

        /// <summary>Clear the state a pooled number must not keep.</summary>
        public void Reset()
        {
            _elapsed = 0f;
            _encounter = null;
            _done = null;

            if (label != null)
            {
                label.text = string.Empty;
                label.alpha = 0f;
            }
        }

        private void Update()
        {
            if (_done == null)
                return;

            if (SimulationService.Instance.Run.Encounter != _encounter)
            {
                Finish();
                return;
            }

            _elapsed += SimulationService.Instance.SimDelta(Time.deltaTime);

            if (DamageNumberMotion.IsFinished(_elapsed, _duration))
            {
                Finish();
                return;
            }

            Apply();
        }

        private void Finish()
        {
            var done = _done;
            Reset();
            done?.Invoke(this);
        }

        private void Apply()
        {
            Rect.anchoredPosition = _origin + new Vector2(0f, DamageNumberMotion.Rise(_elapsed, _duration, _height));
            label.alpha = DamageNumberMotion.Alpha(_elapsed, _duration, _hold);
        }
    }
}
