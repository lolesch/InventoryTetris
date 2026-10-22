namespace ToolSmiths.InventorySystem.Data
{
    /// <summary>
    /// The Assets > Create menu paths of the project's ScriptableObjects, so the menu stays one tree.
    /// All <c>const</c>, so they can feed <c>[CreateAssetMenu(menuName = ...)]</c>.
    /// </summary>
    public static class AssetMenus
    {
        public const string Root = "Inventory System/";

        public const string Distributions = Root + "Probability Distributions/";
        public const string Hero = Root + "Hero/";
        public const string Combat = Root + "Combat/";
    }
}
