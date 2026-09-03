using System.Collections.Generic;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Items
{
    /// <summary>
    /// The authored <see cref="IItemCatalog"/> - the single asset that aggregates every
    /// <see cref="ItemDefinitionAsset"/> in the project. Replaces the ~20
    /// <c>List&lt;AbstractItemObject&gt;</c> fields on <c>ItemProvider</c>: the generator
    /// reads it to pick what to roll, a display reads it to resolve a stored instance back
    /// to its template.
    ///
    /// Tests use <c>InMemoryItemCatalog</c> instead - two adapters behind one interface, a
    /// real seam. Both honour the same loud-failure contract: an unknown id throws
    /// <see cref="KeyNotFoundException"/>, never a silent null.
    /// </summary>
    [CreateAssetMenu(fileName = "Item Catalog", menuName = "Inventory System/Item Catalog")]
    public sealed class ItemCatalogAsset : ScriptableObject, IItemCatalog
    {
        [SerializeField] private List<ItemDefinitionAsset> definitions = new();

        [System.NonSerialized] private Dictionary<string, ItemDefinition> byId;

        /// <inheritdoc/>
        public ItemDefinition Definition(string id)
        {
            byId ??= BuildIndex();
            return byId.TryGetValue(id, out var definition)
                ? definition
                : throw new KeyNotFoundException($"no item definition with id '{id}'");
        }

        /// <inheritdoc/>
        public IEnumerable<ItemDefinition> OfCategory(ItemCategory category)
        {
            // Serve from the same deduped/validated index Definition() uses, so the two
            // methods never disagree - and so this real adapter matches InMemoryItemCatalog,
            // whose OfCategory also walks the index. A slot BuildIndex rejected (null,
            // blank id, duplicate id) must not be pickable here.
            byId ??= BuildIndex();
            foreach (var definition in byId.Values)
                if (definition.Category == category)
                    yield return definition;
        }

        /// <summary>Every non-empty definition slot, for editor and debug use.</summary>
        public IReadOnlyList<ItemDefinitionAsset> Definitions => definitions;

        private void OnEnable() => byId = null;
        private void OnValidate() => byId = null;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only authoring seam - used by the <c>UniquesMigration</c> script to fill
        /// the catalog and by the item-content tests. The runtime never mutates a catalog.
        /// </summary>
        internal void SetDefinitions(IEnumerable<ItemDefinitionAsset> items)
        {
            definitions = new List<ItemDefinitionAsset>(items);
            byId = null;
        }
#endif

        private Dictionary<string, ItemDefinition> BuildIndex()
        {
            var index = new Dictionary<string, ItemDefinition>(definitions.Count);

            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];

                if (definition == null)
                {
                    Debug.LogError($"{name}: definition slot {i} is empty", this);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.Id))
                {
                    Debug.LogError($"{name}: '{definition.name}' carries no id", definition);
                    continue;
                }

                if (index.ContainsKey(definition.Id))
                {
                    Debug.LogError($"{name}: duplicate definition id '{definition.Id}' ('{definition.name}')", definition);
                    continue;
                }

                index[definition.Id] = definition;
            }

            return index;
        }
    }
}
