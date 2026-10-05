using System;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Binds <see cref="ISettlementLedger"/> to a <see cref="Hero"/> and its <c>Wallet</c>. Built
    /// over the hero the settlement is for, like <see cref="ContainerSettlementBag"/>.
    /// </summary>
    public sealed class PlayerWalletLedger : ISettlementLedger
    {
        private readonly Hero _hero;

        public PlayerWalletLedger(Hero hero) =>
            _hero = hero ?? throw new ArgumentNullException(nameof(hero));

        public void ChargeFee(long baseUnits)
        {
            var fee = new Currency((uint)baseUnits);
            if (fee.Total > 0u)
                _ = _hero.Wallet.TryPay(fee);
        }

        public void ForfeitXp(int xp)
        {
            if (xp > 0)
                _ = _hero.GetResource(StatName.Experience).RemoveFromCurrent(xp);
        }

        public void ReviveIfDown()
        {
            if (!_hero.IsDead)
                return;

            _hero.Heal();
        }
    }
}
