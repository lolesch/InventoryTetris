using System;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// The Hero's Corpse as it is saved: the stable id of the Location it lies at, and its items.
    /// An empty <see cref="locationId"/> means no Corpse. (<c>JsonUtility</c> writes an unset nested
    /// object as an empty one, so absence is carried by the id, not by a null.)
    /// </summary>
    [Serializable]
    public sealed class CorpseDto
    {
        public string locationId = string.Empty;
        public ItemInstanceDto[] items = Array.Empty<ItemInstanceDto>();
    }
}
