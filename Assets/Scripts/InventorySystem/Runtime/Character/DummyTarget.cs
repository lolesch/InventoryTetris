using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class DummyTarget : BaseCharacter
    {
        [SerializeField] private uint experience = 20; // TODO: derive from monsterLevel and combat rating?

        protected override void OnDeath()
        {
            Debug.LogWarning($"{name.ColoredComponent()} {"died!".Colored(Color.red)}", this);

            var loot = ItemProvider.Instance.RollLoot();

            foreach (var package in loot)
                //rework to drop items on the floor
                _ = CharacterProvider.Instance.Player.PickUpItem(package);

            // TODO: use event instead?
            CharacterProvider.Instance.Player.GainExperience(experience, CharacterLevel);

            this.GetResource(StatName.Health).RefillCurrent();
            this.GetResource(StatName.Shield).RefillCurrent();
        }
    }
}
