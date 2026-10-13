using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>The hero's level: "Level 12".</summary>
    public sealed class HeroLevelDisplay : HeroIdentityDisplay
    {
        protected override string Format(HeroIdentity identity) => $"Level {identity.Level}";
    }
}
