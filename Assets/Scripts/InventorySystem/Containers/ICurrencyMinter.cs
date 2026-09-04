using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// Mints a single coin of a denomination as an <see cref="ItemInstance"/>. The wallet
    /// logic on <see cref="CharacterInventory"/> pays change back in coins and used to
    /// reach <c>ItemProvider.Instance.MintCurrency(...)</c> to do it; injected now so the
    /// container assembly names no provider. Implemented by <c>ItemProvider</c>.
    /// </summary>
    public interface ICurrencyMinter
    {
        ItemInstance MintCurrency(CurrencyType type);
    }
}
