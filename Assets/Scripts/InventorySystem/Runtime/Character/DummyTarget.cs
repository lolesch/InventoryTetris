using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public sealed class DummyTarget : BaseCharacter
    {
        [SerializeField] private uint experience = 20; // TODO: derive from monsterLevel and combat rating?

        protected override void OnDeath()
        {
            Debug.LogWarning($"{name.ColoredComponent()} {"died!".Colored(Color.red)}", this);

            var player = CharacterProvider.Instance.Player;
            var loot = ItemService.Instance.RollLoot(
                magicFind: player.GetStatValue(StatName.IncreasedItemRarity),
                itemQuantity: player.GetStatValue(StatName.IncreasedItemQuantity));

            foreach (var package in loot)
                //rework to drop items on the floor
                _ = player.PickUpItemOrStash(package);

            // TODO: use event instead?
            player.GainExperience(experience, CharacterLevel);

            this.GetResource(StatName.Health).RefillCurrent();
            this.GetResource(StatName.Shield).RefillCurrent();
        }
    }
}
