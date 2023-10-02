using System.Linq;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    public abstract class AbstractProbabilityDistribution<T> : ScriptableObject where T : System.Enum
    {
        [System.Serializable]
        public struct EnumerationQuantity
        {
            [HideInInspector, SerializeField] public string name;
            [HideInInspector, SerializeField] public T Enumeration;
            [SerializeField] public uint Quantity;

            public EnumerationQuantity(T enumeration, uint quantity)
            {
                Enumeration = enumeration;
                name = Enumeration.ToString();
                Quantity = quantity;
            }
        }

        [System.Serializable]
        public struct EnumerationProbability
        {
            [HideInInspector, SerializeField] public string name;
            [HideInInspector, SerializeField] public T Enumeration;
            [SerializeField, Range(0f, 1f)] public float Probability;

            public EnumerationProbability(T enumeration, Vector2 fraction) => this = new EnumerationProbability(enumeration, fraction.x / fraction.y);
            public EnumerationProbability(T enumeration, float fraction)
            {
                Enumeration = enumeration;
                name = Enumeration.ToString();
                Probability = fraction;
            }
        }

        [SerializeField] private uint failQuantity = 0;
        [SerializeField] private EnumerationQuantity[] quantities = new EnumerationQuantity[System.Enum.GetValues(typeof(T)).Length];
        [SerializeField, ReadOnly, Range(0f, 1f)] private float successProbability;
        [SerializeField, ReadOnly] private EnumerationProbability[] probabilities;

        [SerializeField, ReadOnly] private T[] exampleResults = new T[10];

        private uint SuccessQuantity => (uint)(quantities.Sum(x => x.Quantity) - quantities[0].Quantity);
        private uint AllySensitiveFailQuantity => (uint)(SuccessQuantity / (1f / Mathf.Pow((float)failQuantity / (failQuantity + SuccessQuantity), GetFailExponent()) - 1f));
        private float QuantitySum => SuccessQuantity + AllySensitiveFailQuantity;
        public EnumerationProbability[] Probabilities
        {
            get
            {
                var array = new EnumerationProbability[quantities.Length];

                for (var i = 0; i < quantities.Length; i++)
                {
                    var fraction = quantities[i].Quantity / Mathf.Max(1, QuantitySum);
                    array[i] = new EnumerationProbability(quantities[i].Enumeration, fraction);
                }
                probabilities = array.OrderBy(x => x.Probability).ToArray();

                return probabilities;
            }
        }

        private void OnValidate()
        {
            if (quantities.Length != System.Enum.GetValues(typeof(T)).Length)
                quantities = new EnumerationQuantity[System.Enum.GetValues(typeof(T)).Length];

            for (var i = 0; i < quantities.Length; i++)
                quantities[i] = new EnumerationQuantity((System.Enum.GetValues(typeof(T)) as T[])[i], quantities[i].Quantity);

            quantities[0] = new EnumerationQuantity(quantities[0].Enumeration, AllySensitiveFailQuantity);
            successProbability = Mathf.Clamp01(Probabilities.Sum(x => x.Probability) - Probabilities[0].Probability) / 1f;

            for (var i = 0; i < exampleResults.Length; i++)
                exampleResults[i] = GetRandomEnumerator();

            exampleResults = exampleResults.OrderBy(x => x).ToArray();
        }

        protected virtual int GetFailExponent() => 1;

        public T GetRandomEnumerator(float externalProbabilityIncrease = 0f)
        {
            var randomRoll = Random.Range(0f, 1f);

            for (var i = 0; i < Probabilities.Length; i++)
            {
                var threshold = 0f;

                for (var j = 0; j <= i + externalProbabilityIncrease / 100; j++)
                    threshold += Probabilities[j].Probability;

                if (randomRoll <= threshold)
                    return Probabilities[i].Enumeration;
            }

            Debug.LogWarning($"Oh oh.. something is wron with {Probabilities[0].Enumeration.GetType().Name}");
            return default;
        }
    }
}
