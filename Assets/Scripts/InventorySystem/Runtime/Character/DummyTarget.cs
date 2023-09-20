using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Utility.Extensions;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class DummyTarget : BaseCharacter
    {
        [SerializeField] private uint experience = 10; // TODO: derive from monsterLevel and combat rating?

        protected override void OnDeath()
        {
            Debug.LogWarning($"{name.ColoredComponent()} {"died!".Colored(Color.red)}", this);

            var randomEquipment = ItemProvider.Instance.GenerateRandomLoot();

            foreach (var item in randomEquipment)
                InventoryProvider.Instance.Inventory.AddToContainer(new Package(null, item, 1));

            // send experience to the player?
            CharacterProvider.Instance.Player.GainExperience(experience);

            this.GetResource(StatName.Health).RefillCurrent();
        }
    }
}
