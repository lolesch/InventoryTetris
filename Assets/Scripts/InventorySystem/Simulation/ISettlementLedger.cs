namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The hero's wallet and character sheet as <see cref="RunSettlement"/> needs to touch them
    /// on a Death (ADR-0009): the currency fee, the forfeited XP, and the revive. The engine
    /// binds this to the live <c>Wallet</c> and <c>LocalPlayer</c>; a test records the calls.
    /// </summary>
    public interface ISettlementLedger
    {
        /// <summary>
        /// Withdraw the Death fee - <paramref name="baseUnits"/> iron-equivalent (<c>CONTEXT.md</c>
        /// <i>Base Unit</i>) - from the Wallet. Never takes more than is there.
        /// </summary>
        void ChargeFee(long baseUnits);

        /// <summary>Subtract <paramref name="xp"/> from the hero's progress toward the next level.</summary>
        void ForfeitXp(int xp);

        /// <summary>
        /// Revive the hero at full Health and full Resource - immediately Sendable (issue #45).
        /// A no-op if the hero is not down.
        /// </summary>
        void ReviveIfDown();
    }
}
