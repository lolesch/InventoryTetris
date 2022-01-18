using UnityEngine;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Tests")]

namespace ToolSmiths.InventorySystem.Inventories
{
    [System.Serializable]
    public class PlayerEquipment : AbstractContainer { public PlayerEquipment(Vector2Int dimensions) : base(dimensions) { } }
}
