using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Distributions
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "Weapon Category Distribution", menuName = AssetMenus.Distributions + "Weapon Category")]
    public sealed class WeaponCategoryDistribution : AbstractProbabilityDistribution<WeaponCategory> { }
}
