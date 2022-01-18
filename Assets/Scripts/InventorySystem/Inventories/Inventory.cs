using UnityEngine;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class Inventory : AbstractContainer { public Inventory(Vector2Int dimensions) : base(dimensions) { } }
}