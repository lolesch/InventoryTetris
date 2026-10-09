using System.Collections.Generic;
using Submodules.Utility.Tools;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The floor as a grid (epic #214, issue #221): one pooled <see cref="GroundItemSlotDisplay"/> per Package
    /// on <see cref="LootFlow.Ground"/>, drawn over the cell it landed in. Entries are keyed by cell, so a
    /// Package never moves when another is picked up or evicted, and a stack that gains items stays put. Each
    /// entry's opacity comes from its age rank among the drops (<see cref="GroundFade"/>): the newest is opaque,
    /// the oldest sits at <see cref="oldestAlpha"/>. A click picks the Drop up through
    /// <see cref="LootFlow.PickUpFromGround"/>, the same path as the list (<see cref="GroundItemsPanel"/>).
    ///
    /// The entries are anchored by fractions of the grid's rect, so they follow its size with no layout pass;
    /// the <see cref="AspectRatioFitter"/> keeps its cells square.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundGridPanel : GroundViewPanel
    {
        [SerializeField] private GroundItemSlotDisplay prefab;
        [SerializeField, Tooltip("The rect the cells are laid over. Square cells need an AspectRatioFitter on it.")]
        private RectTransform grid;
        [SerializeField, Tooltip("Keeps the grid's cells square by taking the ground's width to height. Optional.")]
        private AspectRatioFitter aspect;
        [SerializeField, Range(0f, 1f), Tooltip("The oldest drop's alpha; the newest is fully opaque.")]
        private float oldestAlpha = 0.35f;

        private readonly Dictionary<Vector2Int, GroundItemSlotDisplay> _entries = new();
        private readonly List<Vector2Int> _gone = new();
        private PrefabPool<GroundItemSlotDisplay> _pool;

        // Lazily built rather than in Awake: see GroundItemsPanel.
        private PrefabPool<GroundItemSlotDisplay> Pool => _pool ??= new PrefabPool<GroundItemSlotDisplay>(prefab, grid);

        protected override void Refresh()
        {
            var ground = Bound?.Ground;

            if (ground == null)
            {
                ReleaseWhere(_ => true);
                return;
            }

            var size = ground.Dimensions;
            if (aspect != null)
                aspect.aspectRatio = size.x / (float)size.y;

            var cells = ground.CellsOldestFirst();
            ReleaseWhere(cell => !ground.StoredPackages.ContainsKey(cell));

            for (var rank = 0; rank < cells.Count; rank++)
            {
                var cell = cells[rank];
                var package = ground.StoredPackages[cell];

                if (!_entries.TryGetValue(cell, out var entry))
                    _entries[cell] = entry = Pool.GetObject();

                // Only the rank-driven alpha changes for an entry whose package stayed put.
                if (!ReferenceEquals(entry.Item, package.Item) || entry.Amount != package.Amount)
                {
                    var view = ItemService.Instance.View(package.Item);
                    entry.Bind(package.Item, package.Amount, view, OnEntryClicked);
                    Place((RectTransform)entry.transform, cell, view.Dimensions, size);
                }

                entry.Fade(GroundFade.Alpha(rank, cells.Count, oldestAlpha));
            }
        }

        // Anchors, not offsets: the cell's corners as fractions of the grid, y counted down from the top.
        private static void Place(RectTransform entry, Vector2Int cell, Vector2Int footprint, Vector2Int size)
        {
            entry.anchorMin = new Vector2(cell.x / (float)size.x, 1f - ((cell.y + footprint.y) / (float)size.y));
            entry.anchorMax = new Vector2((cell.x + footprint.x) / (float)size.x, 1f - (cell.y / (float)size.y));
            entry.offsetMin = entry.offsetMax = Vector2.zero;
        }

        private void OnEntryClicked(ItemInstance item) => _ = Bound?.PickUpFromGround(item);

        private void ReleaseWhere(System.Predicate<Vector2Int> release)
        {
            _gone.Clear();
            foreach (var cell in _entries.Keys)
                if (release(cell))
                    _gone.Add(cell);

            foreach (var cell in _gone)
            {
                Release(_entries[cell]);
                _ = _entries.Remove(cell);
            }
        }

        // OnDisable also runs as Play mode tears the scene down, when an entry may already be
        // destroyed - releasing one would throw, and one exception pauses the Editor.
        private void Release(GroundItemSlotDisplay entry)
        {
            if (entry == null)
                return;

            entry.Unbind();
            Pool.ReleaseObject(entry);
        }
    }
}
