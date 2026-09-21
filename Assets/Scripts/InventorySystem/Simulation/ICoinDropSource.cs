using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// Rolls one coin Pile per kill — a denomination and a pile size (spec "Loot flow";
    /// CONTEXT.md "Pile"). Owns its own randomness, the same seam <c>ItemGenerator</c> (via
    /// <c>InventorySystem.Items.IRollSource</c>) already keeps constructor-side rather than
    /// per-call, so a Pile roll composes the same way an item roll does.
    ///
    /// The production adapter (issue #26) wraps the existing <c>CurrencyTypeDistribution</c>
    /// and <c>CurrencyDropTable</c> ScriptableObjects; a test scripts a fixed or queued
    /// stand-in. An <c>Amount</c> of 0 means no coins fell this kill (the fail bucket).
    /// </summary>
    public interface ICoinDropSource
    {
        (CurrencyType Type, uint Amount) RollPile();
    }
}
