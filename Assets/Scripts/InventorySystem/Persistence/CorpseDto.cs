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

        /// <summary>Whether <see cref="fallX"/> and <see cref="fallZ"/> were recorded; a save from before they were has neither and loads at the Origin.</summary>
        public bool hasFall;
        public float fallX;
        public float fallZ;

        public ItemInstanceDto[] items = Array.Empty<ItemInstanceDto>();
    }
}
