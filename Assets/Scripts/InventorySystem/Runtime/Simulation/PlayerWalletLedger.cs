using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Simulation;
using ToolSmiths.InventorySystem.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Binds <see cref="ISettlementLedger"/> to the live <c>Wallet</c> and <c>LocalPlayer</c>.
    /// Stateless: each call resolves its provider, so the one long-lived
    /// <see cref="RunSettlement"/> always acts on the current hero.
    /// </summary>
    public sealed class PlayerWalletLedger : ISettlementLedger
    {
        public void ChargeFee(long baseUnits)
        {
            var fee = new Currency((uint)baseUnits);
            if (fee.Total > 0u)
                _ = InventoryProvider.Instance.Wallet.TryPay(fee);
        }

        public void ForfeitXp(int xp)
        {
            var player = CharacterProvider.Instance.Player;
            if (player != null && xp > 0)
                _ = player.GetResource(StatName.Experience).RemoveFromCurrent(xp);
        }

        public void ReviveIfDown()
        {
            var player = CharacterProvider.Instance.Player;
            if (player == null || !player.IsDead)
                return;

            player.GetResource(StatName.Health).RefillCurrent();
            player.GetResource(StatName.Resource).RefillCurrent();
        }
    }
}
