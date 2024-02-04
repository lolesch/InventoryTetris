using UnityEngine;

namespace ToolSmiths.InventorySystem.Data.Skills
{
    [CreateAssetMenu(fileName = "New Skill Object", menuName = "Inventory System/Skills/Skill")]
    public class Skill : ScriptableObject
    {
        [SerializeField] private SpawnData spawnData;
        public SpawnData SpawnData => spawnData;
    }
}
