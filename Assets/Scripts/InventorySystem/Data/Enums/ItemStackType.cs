namespace ToolSmiths.InventorySystem.Data.Enums
{
    [System.Serializable]
    /// The chategory of item stackLimits
    public enum ItemStackType
    {
        NONE = 0,
        Single = 1,
        StackOfTen = 10,
        StackOfFifty = 50,
        StackOfHundred = 100,
    }
}