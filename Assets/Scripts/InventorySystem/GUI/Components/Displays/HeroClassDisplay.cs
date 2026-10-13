using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.GUI.Displays
{
    /// <summary>The hero's class: the display name of its template.</summary>
    public sealed class HeroClassDisplay : HeroIdentityDisplay
    {
        protected override string Format(HeroIdentity identity) => identity.ClassName;
    }
}
