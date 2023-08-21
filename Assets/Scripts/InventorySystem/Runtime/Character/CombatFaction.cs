using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    [CreateAssetMenu(menuName = "GrimbartTales/Combat/Faction")]
    public class CombatFaction : ScriptableObject
    {
        public CombatFaction[] EnemyFactions;
        public CombatFaction[] AllyFactions;

        public bool IsEnemy(CombatFaction faction)
        {
            for (var i = 0; i < EnemyFactions.Length; i++)
                if (EnemyFactions[i] == faction)
                    return true;

            return false;
        }

        public bool IsAlly(CombatFaction faction)
        {
            if (faction == this)
                return true;

            for (var i = 0; i < AllyFactions.Length; i++)
                if (AllyFactions[i] == faction)
                    return true;

            return false;
        }
    }
}
