using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Item Category Distribution", menuName = "Inventory System/Items/Distributions/Item Category")]
    public class ItemCategoryDistribution : AbstractProbabilityDistribution<ItemCategory> { }
}
