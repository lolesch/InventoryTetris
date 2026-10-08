using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// What a weapon type means on the ground (spatial-combat spec): the hero's Strike Range is a base property
    /// of the type he wields, never a stat, so gear cannot roll extra reach and range does not become the one
    /// stat every build must stack. An unarmed hero (or one holding no weapon) has
    /// <see cref="GroundTuning.HeroStrikeRange"/> instead. The numbers are untested starting points, in
    /// ground units, to be tuned in play.
    /// </summary>
    public static class WeaponTypes
    {
        /// <summary>
        /// The Strike Range of <paramref name="type"/>, or <c>null</c> when it is not a weapon.
        /// </summary>
        public static float? StrikeRange(EquipmentType type) => type switch
        {
            EquipmentType.Sword => 2f,
            EquipmentType.GreatSword => 2.5f,
            EquipmentType.Bow => 6f,
            EquipmentType.Crossbow => 7f,
            _ => null,
        };
    }
}
