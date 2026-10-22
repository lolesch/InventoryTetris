using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// Looks a definition up by id, and lists the definitions in a category. The generator
    /// reads it to pick what to roll; a display reads it to resolve a stored instance back
    /// to its template.
    ///
    /// Two adapters sit behind this, a real seam: <c>ItemCatalogAsset : ScriptableObject</c>
    /// aggregating every authored <c>ItemDefinitionAsset</c> (issue #7), and an in-memory
    /// dictionary the tests use.
    /// </summary>
    public interface IItemCatalog
    {
        /// <summary>
        /// The definition with this id. Throws <see cref="KeyNotFoundException"/> when no
        /// definition has it - a stored instance pointing at a deleted definition is a
        /// loud failure, never a silent null.
        /// </summary>
        ItemDefinition Definition(string id);

        /// <summary>Every definition in the given category, in no guaranteed order. Empty when none match.</summary>
        IEnumerable<ItemDefinition> OfCategory(ItemCategory category);
    }
}
