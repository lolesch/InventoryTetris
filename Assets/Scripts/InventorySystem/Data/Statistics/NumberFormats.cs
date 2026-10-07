using System;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// Every number the UI prints goes through one of these, so how many decimals a stat shows is
    /// one edit. The strings are <c>static readonly</c> (built from <see cref="StatDigits"/>), so
    /// pass them to <c>ToString(format)</c> - they cannot sit inside an interpolation hole's
    /// <c>:format</c> or an attribute.
    /// </summary>
    public static class NumberFormats
    {
        /// <summary>Decimals a stat figure is rounded to and printed with. The one number to edit.</summary>
        public const int StatDigits = 2;

        /// <summary>A stat figure: at most <see cref="StatDigits"/> decimals, a leading zero, no trailing zeros ("0.14", "12").</summary>
        public static readonly string Stat = "0." + new string('#', StatDigits);

        /// <summary><see cref="Stat"/> with an explicit sign and a space: "+ 0.14", "- 3", "0".</summary>
        public static readonly string StatSigned = $"+ {Stat};- {Stat};{Stat}";

        /// <summary><see cref="Stat"/> that only marks a negative, for a value that replaces rather than adds: "5", "- 5".</summary>
        public static readonly string StatNegativeOnly = $"{Stat};- {Stat};{Stat}";

        /// <summary>A resource pool's current/total and percent: one optional decimal.</summary>
        public const string Resource = "0.#";

        /// <summary>A recovery rate per second: one optional decimal, so zero reads "0".</summary>
        public const string Rate = "0.#";

        /// <summary>A duration in seconds: one optional decimal.</summary>
        public const string Seconds = "0.#";

        /// <summary>A percentage figure in a tool or readout: one optional decimal.</summary>
        public const string Percent = "0.#";

        /// <summary>Rounds to <see cref="StatDigits"/>, so a value too small to print is exactly zero.</summary>
        public static float RoundStat(float value) => (float)Math.Round(value, StatDigits);
    }
}
