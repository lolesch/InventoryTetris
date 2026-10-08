namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// How the hero picks the enemy he fights (spatial-combat spec): a pattern chosen by an enum so a skill
    /// can swap it later. <see cref="EncounterTuning.HeroTargeting"/> holds the choice.
    /// </summary>
    public enum HeroTargeting
    {
        /// <summary>
        /// Keep one target until it falls. With none, take the living enemy with the lowest
        /// <c>w * distance from the origin + (1 - w) * distance from the hero</c>, <c>w</c> being
        /// <see cref="HeroBehaviour.OriginWeight"/>, the earliest spawned on a tie. A target outside his
        /// Strike Range gives way to the best-scoring enemy inside it, if there is one.
        /// </summary>
        WeightedProximity = 0,
    }
}
