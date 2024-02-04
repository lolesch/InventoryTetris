using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Weapon Category Distribution", menuName = "Inventory System/Items/Distributions/Weapon Category")]
    public class WeaponCategoryDistribution : AbstractProbabilityDistribution<WeaponCategory> { }
}
