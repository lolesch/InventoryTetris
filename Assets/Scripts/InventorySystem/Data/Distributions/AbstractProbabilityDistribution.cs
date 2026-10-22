using System.Collections.Generic;
using ToolSmiths.InventorySystem.Probability;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    /// <summary>
    /// Non-generic root so a single <c>[CustomEditor(typeof(AbstractProbabilityDistribution), true)]</c>
    /// can draw every closed distribution — Unity cannot target an open generic. All
    /// behaviour lives in <see cref="AbstractProbabilityDistribution{T}"/>.
    /// </summary>
    public abstract class AbstractProbabilityDistribution : ScriptableObject
    {
        /// <summary>Outcome names in enum-declaration order — row labels for the inspector.</summary>
        public abstract IReadOnlyList<string> OutcomeNames { get; }

        /// <summary>The current probability vector, enum order, summing to 1. Derived, never serialized.</summary>
        public abstract IReadOnlyList<float> Probabilities { get; }

        /// <summary>Rolls once against <paramref name="roll"/> in [0,1]; returns the outcome's name — inspector sample preview.</summary>
        public abstract string SampleName(float roll);
    }

    public abstract class AbstractProbabilityDistribution<T> : AbstractProbabilityDistribution where T : System.Enum
    {
        [System.Serializable]
        public struct EnumerationQuantity
        {
            [HideInInspector, SerializeField] public string name;
            [HideInInspector, SerializeField] public T Enumeration;
            [SerializeField, Min(0)] public uint Quantity;

            public EnumerationQuantity(T enumeration, uint quantity)
            {
                Enumeration = enumeration;
                name = enumeration.ToString();
                Quantity = quantity;
            }
        }

        private static readonly T[] Values = (T[])System.Enum.GetValues(typeof(T));

        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("failQuantity")]
        private uint failWeight = 0;
        [SerializeField] private EnumerationQuantity[] quantities = FreshQuantities();

        [System.NonSerialized] private ProbabilityTable<T> _table;
        private ProbabilityTable<T> Table => _table ??= BuildTable();

        private ProbabilityTable<T> BuildTable()
        {
            var weights = new float[Values.Length];
            for (var i = 0; i < weights.Length && i < quantities.Length; i++)
                weights[i] = quantities[i].Quantity;

            return new ProbabilityTable<T>(weights, failWeight, GetFailExponent());
        }

        // ── inspector surface: all derived, nothing serialized ──
        public override IReadOnlyList<string> OutcomeNames => System.Array.ConvertAll(Values, v => v.ToString());
        public override IReadOnlyList<float> Probabilities => Table.Probabilities;
        public override string SampleName(float roll) => Table.Sample(roll).ToString();

        public float ProbabilityOf(T outcome) => Table.ProbabilityOf(outcome);

        /// <summary>The ally-scaling exponent on the fail probability. 1 on the generic base.</summary>
        protected virtual float GetFailExponent() => 1f;

        /// <summary>Rolls one outcome from the authored table.</summary>
        public T Roll() => Table.Sample(UnityEngine.Random.Range(0f, 1f));

        private void OnValidate()
        {
            quantities = Migrate(quantities);
            _table = null; // rebuilt lazily against the new data — never baked into a field
        }

        private static EnumerationQuantity[] FreshQuantities() =>
            System.Array.ConvertAll(Values, v => new EnumerationQuantity(v, 0u));

        private static EnumerationQuantity[] Migrate(EnumerationQuantity[] current)
        {
            current ??= System.Array.Empty<EnumerationQuantity>();

            var oldOutcomes = System.Array.ConvertAll(current, q => q.Enumeration);
            var oldWeights = System.Array.ConvertAll(current, q => (float)q.Quantity);
            var remapped = WeightMigration.Remap(oldOutcomes, oldWeights, Values);

            var next = new EnumerationQuantity[Values.Length];
            for (var i = 0; i < Values.Length; i++)
                next[i] = new EnumerationQuantity(Values[i], (uint)System.Math.Max(0, System.Math.Round(remapped[i])));
            return next;
        }
    }
}
