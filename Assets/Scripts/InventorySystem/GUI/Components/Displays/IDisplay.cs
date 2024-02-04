namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public interface IDisplay<T> where T : struct
    {
        void RefreshDisplay(T data);
    }
}