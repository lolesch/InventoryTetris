using UnityEngine;

namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// What the player tunes to taste, kept per machine in <c>PlayerPrefs</c> (a hero save is not the
    /// place: these follow the player, not the character). Every setting has its default and its
    /// bounds here, so a settings screen reads the range from the same place the game clamps to.
    /// </summary>
    public static class UserSettings
    {
        internal const string HoverDelayKey = "settings.hoverDelay";
        internal const string GroundItemFadeDelayKey = "settings.groundItemFadeDelay";

        public const float DefaultHoverDelay = 0.3f;
        public const float MinHoverDelay = 0f;
        public const float MaxHoverDelay = 1.5f;

        public const float DefaultGroundItemFadeDelay = 60f;
        public const float MinGroundItemFadeDelay = 5f;
        public const float MaxGroundItemFadeDelay = 600f;

        /// <summary>Seconds the cursor rests on an item before its tooltip shows.</summary>
        public static float HoverDelay
        {
            get => Read(HoverDelayKey, DefaultHoverDelay, MinHoverDelay, MaxHoverDelay);
            set => Write(HoverDelayKey, value, MinHoverDelay, MaxHoverDelay);
        }

        /// <summary>
        /// Seconds an item dropped to the ground lies there before it fades out. Stored and bounded,
        /// but nothing reads it yet: the ground has no fade-out.
        /// </summary>
        public static float GroundItemFadeDelay
        {
            get => Read(GroundItemFadeDelayKey, DefaultGroundItemFadeDelay, MinGroundItemFadeDelay, MaxGroundItemFadeDelay);
            set => Write(GroundItemFadeDelayKey, value, MinGroundItemFadeDelay, MaxGroundItemFadeDelay);
        }

        /// <summary>
        /// Writes the settings to disk. A set only changes the in-memory value; Unity flushes on a clean
        /// exit, so a settings screen calls this when the player commits a change (a slider released),
        /// not on every drag step.
        /// </summary>
        public static void Save() => PlayerPrefs.Save();

        // NaN slips through Mathf.Clamp unchanged, so it is refused here: a stored NaN would reach
        // WaitForSeconds and every consumer of the setting.
        private static float Read(string key, float fallback, float min, float max)
        {
            var stored = PlayerPrefs.GetFloat(key, fallback);

            return float.IsNaN(stored) ? fallback : Mathf.Clamp(stored, min, max);
        }

        private static void Write(string key, float value, float min, float max)
        {
            if (!float.IsNaN(value))
                PlayerPrefs.SetFloat(key, Mathf.Clamp(value, min, max));
        }
    }
}
