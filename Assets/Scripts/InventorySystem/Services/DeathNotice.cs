using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What the Death notice says after a Death (spec <i>hero travel</i>, ADR-0021): where the hero fell, what the
    /// Run's result cost him and that the bag waits there as the Corpse. The amounts are the Run's own, the fee in coins.
    /// </summary>
    public static class DeathNotice
    {
        /// <param name="location">Where the hero fell; null or empty when no Location was selected, which reads as "the field".</param>
        public static string TextFor(string location, int xpLost, long fee) =>
            $"You fell in {(string.IsNullOrEmpty(location) ? "the field" : location)}. You lost {xpLost} XP and a fee of {FeeText(fee)}. Your bag lies where you fell; go back to pick it up.";

        /// <summary>The fee in the ladder's coins, largest first and only the ones it holds: "1 gold, 3 silver and 4 iron".</summary>
        public static string FeeText(long fee)
        {
            var coins = new Currency((uint)fee);
            var parts = new List<string>();

            foreach (var type in Currency.Denominations)
                if (coins.CountOf(type) != 0u)
                    parts.Add($"{coins.CountOf(type)} {type.ToString().ToLowerInvariant()}");

            return parts.Count switch
            {
                0 => "0 iron",
                1 => parts[0],
                _ => $"{string.Join(", ", parts.GetRange(0, parts.Count - 1))} and {parts[parts.Count - 1]}",
            };
        }
    }
}
