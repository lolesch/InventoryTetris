namespace ToolSmiths.InventorySystem.EditorScripts
{
    /// <summary>
    /// The Editor menu bar paths of the project's tools, so the ToolSmiths menu stays one tree.
    /// All <c>const</c>, so they can feed <c>[MenuItem]</c>. (<c>BundleVersionSetter</c> in the
    /// <c>Utility</c> submodule keeps its own <c>ToolSmiths/Version</c> paths: it cannot see this project.)
    /// </summary>
    internal static class EditorMenus
    {
        public const string Root = "ToolSmiths/";

        public const string Hero = Root + "Hero/";
        public const string HeroSaves = Root + "Hero Saves/";
        public const string Tests = Root + "Tests/";
    }
}
