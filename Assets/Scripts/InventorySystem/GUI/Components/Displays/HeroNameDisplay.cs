using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>The hero's name, as the player typed it.</summary>
    public sealed class HeroNameDisplay : HeroIdentityDisplay
    {
        protected override string Format(HeroIdentity identity) => identity.Name;
    }
}
