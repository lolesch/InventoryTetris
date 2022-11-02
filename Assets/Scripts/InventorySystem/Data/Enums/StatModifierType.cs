namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// Values are the order the modifiers are applied
    public enum StatModifierType
    {
        Override = 0,
        FlatAdd = 100,
        PercentAdd = 200,
        PercentMult = 300,
    }
}