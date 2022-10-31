namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// Values are the order the modifiers are applied
    public enum StatModifierType
    {
        FlatAdd = 100,
        PercentAdd = 200,
        PercentMult = 300,
        Override = int.MaxValue,
    }
}