using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>Where on the hero a <see cref="CorpseEntry"/> lay before the Death.</summary>
    public enum CorpseSource
    {
        /// <summary>No remembered place (a save from before the Corpse kept one): recovery places it as a fresh pick-up.</summary>
        Unknown,
        Bag,
        Equipment,
    }

    /// <summary>
    /// One package on the Corpse - the item, its stack size and the container cell it lay in - so a
    /// recovery can put it back where it was. Cells are plain ints: this module cannot see the engine's vectors.
    /// </summary>
    public readonly struct CorpseEntry
    {
        public CorpseEntry(ItemInstance item, uint amount, CorpseSource source = CorpseSource.Unknown, int x = 0, int y = 0)
        {
            Item = item;
            Amount = amount;
            Source = source;
            X = x;
            Y = y;
        }

        public ItemInstance Item { get; }
        public uint Amount { get; }
        public CorpseSource Source { get; }
        public int X { get; }
        public int Y { get; }

        /// <summary>The same place with <paramref name="amount"/> left, for what a recovery could not put back.</summary>
        public CorpseEntry WithAmount(uint amount) => new(Item, amount, Source, X, Y);
    }
}
