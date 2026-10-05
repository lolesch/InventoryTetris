using Submodules.Utility.Provider;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Inventories
{
    /// <summary>
    /// What is left of the character provider: the scene's debug buttons (Kill, spend-resource)
    /// over the <see cref="LocalPlayer"/> face. Nothing reads the hero through it any more - callers
    /// ask <c>Session.Instance.Hero</c> - and the Healer's refill is the Hero's, wired where the
    /// Session is built. #118 replaces the buttons and #119 deletes this class.
    /// </summary>
    public sealed class CharacterProvider : AbstractProvider<CharacterProvider>
    {
        [field: SerializeField] public LocalPlayer Player { get; private set; }

        public void KillPlayer() => Player.Hero.GetResource(StatName.Health).DepleteCurrent();

        public void ToggleSpendingResource() => Player.SpendResource = !Player.SpendResource;
    }
}
