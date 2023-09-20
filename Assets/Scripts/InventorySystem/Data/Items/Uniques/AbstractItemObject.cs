using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    [System.Serializable]
    public abstract class AbstractItemObject : ScriptableObject
    {
        public abstract AbstractItem GetItem();
    }
}