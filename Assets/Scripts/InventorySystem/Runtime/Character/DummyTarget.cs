using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public class DummyTarget : BaseCharacter
    {
        protected override void OnDeath()
        {
            base.OnDeath();

            var randomEquipment = ItemProvider.Instance.GenerateRandomLoot();

            foreach (var item in randomEquipment)
                InventoryProvider.Instance.Inventory.AddToContainer(new Package(null, item, 1));

            GetResource(this, StatName.Health).RefillCurrent();
        }
    }
}
