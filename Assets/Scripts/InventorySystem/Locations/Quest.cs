using ToolSmiths.InventorySystem.Data;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Locations
{
    /// <summary>
    /// An authored Unlockable with one objective: another Unlockable's id, met when that one Completes while
    /// this Quest is Active (ADR-0023). Objectives of other kinds are out of scope.
    /// </summary>
    [CreateAssetMenu(fileName = "New Quest", menuName = AssetMenus.Root + "Quest")]
    public sealed class Quest : Unlockable
    {
        [Tooltip("The id of the Unlockable whose Complete Completes this Quest, if the Quest was Active before it.")]
        [SerializeField] private string objectiveId;

        public string ObjectiveId => objectiveId;
    }
}
