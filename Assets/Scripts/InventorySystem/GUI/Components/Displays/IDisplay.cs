namespace ToolSmiths.InventorySystem.GUI.Displays
{
    public interface IDisplay<T>
    {
        void RefreshDisplay(T data);
    }
}