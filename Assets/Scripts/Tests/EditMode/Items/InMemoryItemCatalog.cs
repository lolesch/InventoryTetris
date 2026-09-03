using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// The dictionary-backed <see cref="IItemCatalog"/> the tests use in place of the
    /// authored <c>ItemCatalogAsset</c>. <see cref="Definition"/> throws
    /// <see cref="KeyNotFoundException"/> for an unknown id - the same loud-failure
    /// contract the real adapter honours.
    /// </summary>
    public sealed class InMemoryItemCatalog : IItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> byId = new();

        public InMemoryItemCatalog(params ItemDefinition[] definitions)
        {
            foreach (var definition in definitions)
                Add(definition);
        }

        public InMemoryItemCatalog Add(ItemDefinition definition)
        {
            byId[definition.Id] = definition;
            return this;
        }

        public ItemDefinition Definition(string id) =>
            byId.TryGetValue(id, out var definition)
                ? definition
                : throw new KeyNotFoundException($"no item definition with id '{id}'");

        public IEnumerable<ItemDefinition> OfCategory(ItemCategory category)
        {
            foreach (var definition in byId.Values)
                if (definition.Category == category)
                    yield return definition;
        }
    }
}
