using System;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// A container as it is saved: one entry per stored package. The grid size is not saved; a
    /// container is built from the current config, so a resized grid is the restore's problem to
    /// report, not the file's. Plain <c>[Serializable]</c> fields and an array, so
    /// <c>JsonUtility</c> reads and writes it. The Inventory, the Stash and the Equipment each get
    /// their own, so the Stash section can later move out of the hero (ADR-0014).
    /// </summary>
    [Serializable]
    public sealed class ContainerDto
    {
        public PackageDto[] packages = Array.Empty<PackageDto>();
    }

    /// <summary>One stored package: the cell it is keyed at, the item, and the stack size.</summary>
    [Serializable]
    public sealed class PackageDto
    {
        public int x;
        public int y;
        public ItemInstanceDto instance;
        public uint amount;
    }
}
