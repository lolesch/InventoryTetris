using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    /// <summary>
    /// Per-coin drop amount ranges. A currency drop rolls its type from
    /// <c>Currency Type Distribution</c>, then its pile size from here. Not an
    /// <see cref="AbstractProbabilityDistribution{T}"/> — it is a range table, not a
    /// probability distribution. Four explicit fields because the CurrencyType enum
    /// is frozen and there are exactly four coins.
    /// </summary>
    [CreateAssetMenu(fileName = "Currency Drop Table", menuName = "Inventory System/Currency Drop Table")]
    public class CurrencyDropTable : ScriptableObject
    {
        [SerializeField] private Vector2Int iron = new(10, 30);
        [SerializeField] private Vector2Int copper = new(4, 12);
        [SerializeField] private Vector2Int silver = new(1, 3);
        [SerializeField] private Vector2Int gold = new(1, 1);

        public Vector2Int RangeFor(CurrencyType type) => type switch
        {
            CurrencyType.Iron => iron,
            CurrencyType.Copper => copper,
            CurrencyType.Silver => silver,
            CurrencyType.Gold => gold,

            CurrencyType.NONE => Vector2Int.zero,
            _ => Vector2Int.zero,
        };

        /// <summary>Rolls a pile size for <paramref name="type"/>. 0 for NONE / an unset range.</summary>
        public uint RollAmount(CurrencyType type)
        {
            var range = RangeFor(type);
            return range == Vector2Int.zero ? 0u : CurrencyDropRoll.Amount(range.x, range.y, Random.value);
        }
    }
}
