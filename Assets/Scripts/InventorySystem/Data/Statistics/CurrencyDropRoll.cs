using System;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// Maps a uniform roll in [0, 1] to a whole amount in [min, max] inclusive,
    /// flat-weighted. Pure and Unity-free so EditMode tests can reach it;
    /// <see cref="Distributions.CurrencyDropTable"/> feeds it UnityEngine.Random.value.
    /// </summary>
    public static class CurrencyDropRoll
    {
        public static uint Amount(int min, int max, float roll01)
        {
            if (max <= min)
                return (uint)Math.Max(0, min);

            var span = max - min + 1;            // number of distinct outcomes
            var offset = (int)(roll01 * span);   // 0 .. span; == span only when roll01 == 1
            if (offset >= span)
                offset = span - 1;               // fold the half-open top back to max

            return (uint)(min + offset);
        }
    }
}
