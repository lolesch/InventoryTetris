using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The production <see cref="ICoinDropSource"/> (issue #44) — rolls one coin Pile per kill
    /// against the same authored tables the rest of the game reads: the
    /// <see cref="CurrencyTypeDistribution"/> draws which denomination fell, the
    /// <see cref="CurrencyDropTable"/> draws how many. Both are the ItemProvider's own assets,
    /// so a kill's coins come off the same odds the shop and the loot rolls use.
    ///
    /// Issued once by <see cref="SimulationProvider"/>; <see cref="RollPile"/> hands back
    /// <c>(NONE, 0)</c> when the type distribution carries its fail weight, which
    /// <see cref="LootFlow"/> already treats as "no coins this kill".
    /// </summary>
    public sealed class CurrencyDropTableCoinSource : ICoinDropSource
    {
        private readonly CurrencyTypeDistribution _types;
        private readonly CurrencyDropTable _amounts;

        public CurrencyDropTableCoinSource(CurrencyTypeDistribution types, CurrencyDropTable amounts)
        {
            _types = types ?? throw new System.ArgumentNullException(nameof(types));
            _amounts = amounts ?? throw new System.ArgumentNullException(nameof(amounts));
        }

        public (CurrencyType Type, uint Amount) RollPile()
        {
            var type = _types.Roll();
            if (type == CurrencyType.NONE)
                return (CurrencyType.NONE, 0u);

            return (type, _amounts.RollAmount(type));
        }
    }
}