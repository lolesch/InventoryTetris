using System;
using System.Globalization;
using System.Text;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// One package a load left out, as a line in the quarantine sidecar: what it was, which container it
    /// came from, why it was skipped, and when. A person reads it to recover the item by hand; the game
    /// never reads it back, and it is not part of the hero Dto.
    /// </summary>
    [Serializable]
    public sealed class QuarantineEntryDto
    {
        public string quarantinedAtUtc;
        public string container;
        public string reason;
        public int x;
        public int y;
        public uint amount;
        public ItemInstanceDto instance;
    }

    /// <summary>Turns what a restore skipped into sidecar text.</summary>
    public static class QuarantineLog
    {
        /// <summary>
        /// One JSON object per line, one line per skipped package, each ending in a newline, so the text
        /// appends to an existing sidecar. Empty for a clean report.
        /// </summary>
        public static string Lines(RestoreReport report, DateTime utcNow)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));

            var stamp = utcNow.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
            var text = new StringBuilder();

            foreach (var skipped in report.Skipped)
            {
                var entry = new QuarantineEntryDto
                {
                    quarantinedAtUtc = stamp,
                    container = skipped.Container.ToString(),
                    reason = skipped.Reason.ToString(),
                    x = skipped.Package.x,
                    y = skipped.Package.y,
                    amount = skipped.Package.amount,
                    instance = skipped.Package.instance,
                };

                _ = text.Append(JsonUtility.ToJson(entry)).Append('\n');
            }

            return text.ToString();
        }
    }
}
