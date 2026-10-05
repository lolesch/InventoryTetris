using System.Linq;
using ToolSmiths.InventorySystem.Inventories;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>The domain-to-Dto half of the container round trip; <see cref="ContainerRestore"/> is the way back.</summary>
    public static class ContainerMapper
    {
        /// <summary>
        /// The container's packages, in cell order so the same contents always write the same
        /// text. Each package keeps the cell it is keyed at: a two-hander is keyed at its weapon
        /// slot only, and restores through the Equipment's own placement to fill both.
        /// </summary>
        public static ContainerDto ToDto(AbstractDimensionalContainer container) => new()
        {
            packages = container.StoredPackages
                .OrderBy(entry => entry.Key.x)
                .ThenBy(entry => entry.Key.y)
                .Select(entry => new PackageDto
                {
                    x = entry.Key.x,
                    y = entry.Key.y,
                    instance = entry.Value.Item.ToDto(),
                    amount = entry.Value.Amount,
                })
                .ToArray(),
        };
    }
}
