using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Runtime.Character
{
    public abstract class BaseCharacter : MonoBehaviour
    {
        /// ADVANCED DAMAGE CONCEPT:
        // BaseCharacter performs an areal attack (DamageZone/Area/Shape)
        // validate all BaseCharacters within that area as targets (Faction)
        // the area deals damage to all targets over its lifespan

        [field: SerializeField] public bool IsInvincible { get; protected set; } = false;
        [field: SerializeField] public bool IsBlocking { get; protected set; } = false;

        [field: SerializeField] public CharacterStat[] CharacterStats { get; protected set; } = new CharacterStat[(System.Enum.GetValues(typeof(StatName)) as StatName[]).Length];
        [field: SerializeField] public CharacterResource[] CharacterResources { get; protected set; } = new CharacterResource[0];

        public event Action OnBlock;

        protected void OnValidate() => Refresh();

        protected void Update() =>
            // COMBAT TICK RATE
            //var interval = 0f;
            //interval += Time.deltaTime;
            //
            //if(interval >= combatTickRate)
            RegenerateHealth();

        private void RegenerateHealth()
        {
            var health = GetResource(this, StatName.MaxLife);

            if (health.IsDepleted || health.CurrentValue == health.TotalValue)
                return;

            health.AddToCurrent(GetStatValue(this, StatName.LifePerSecond) * Time.deltaTime);
        }

        private void Refresh()
        {
            var statNames = System.Enum.GetValues(typeof(StatName)) as StatName[];
            var statsOnly = statNames.ToList();
            statsOnly.Remove(StatName.MaxLife);
            var resourcesOnly = new List<StatName>() { StatName.MaxLife };

            if (CharacterStats.Length != statsOnly.Count)
            {
                CharacterStats = new CharacterStat[statsOnly.Count];

                for (var i = 0; i < statsOnly.Count; i++)
                    CharacterStats[i] = new CharacterStat(statsOnly[i], 1);
            }

            if (CharacterResources.Length != resourcesOnly.Count)
            {
                //CharacterResources = new CharacterResource[1] { new CharacterResource(StatName.MaxLife, 100) };

                CharacterResources = new CharacterResource[resourcesOnly.Count];

                for (var i = 0; i < resourcesOnly.Count; i++)
                    CharacterResources[i] = new CharacterResource(resourcesOnly[i], 100);
            }
        }

        protected static float CalculateDamageOutput(BaseCharacter character, DamageType damageType)
        {
            var weaponDamage = GetStatValue(character, StatName.WeaponDamage);
            var attackSpeed = GetStatValue(character, StatName.AttackSpeed);
            var damageTypeMod = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(character, StatName.PhysicalDamage),
                DamageType.ElementalDamage => GetStatValue(character, StatName.ElementalDamage),

                _ => 0f,
            };

            return weaponDamage * (1f + attackSpeed * 0.01f) * (1f + damageTypeMod * 0.01f);
            // TODO: if attackSpeed has only percantMods and a base of 0 it will return 0 
        }

        public static float CalculateReceivingDamage(BaseCharacter character, DamageType damageType, float incomingDamage)
        {
            if (character.IsInvincible)
                return 0f;

            if (GetResource(character, StatName.MaxLife).IsDepleted)
                return 0f;

            var damageTypeResist = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(character, StatName.PhysicalResistance) + GetStatValue(character, StatName.Armor) * 0.01f,
                DamageType.ElementalDamage => GetStatValue(character, StatName.ElementalResistance),

                _ => 0f,
            };

            // TODO: Desing defenses
            // Avoidance (block, dodge, barrier absorbtion)
            // Mitigation (resistances, armor)
            // Recovery

            var resistedDamage = incomingDamage * (1f - damageTypeResist * 0.01f);

            // TODO: barrier absorbs damage => deal damage to barrier
            var unshieldedDamage = resistedDamage;// - GetResource(character,StatName.Shield).CurrentValue;

            if (character.IsBlocking)
                unshieldedDamage *= 0.5f; // TODO: design blocking

            return unshieldedDamage;
        }

        protected void DealDamageTo(BaseCharacter target, DamageType damageType)
        {
            var damageOutput = CalculateDamageOutput(this, damageType);
            target.ReceiveDamage(damageType, damageOutput);

            //AddDealtDPS(damageOutput);
        }

        protected void ReceiveDamage(DamageType damageType, float incomingDamage)
        {
            var healthDamage = CalculateReceivingDamage(this, damageType, incomingDamage);
            var health = GetResource(this, StatName.MaxLife);
            health.AddToCurrent(healthDamage);

            //AddReceivedDPS(healthDamage);
        }

        protected static CharacterStat GetStat(BaseCharacter character, StatName stat)
        {
            var statsAndResources = character.CharacterStats.Union(character.CharacterResources).ToArray();
            for (var i = statsAndResources.Length; i-- > 0;)
                if (statsAndResources[i].Stat == stat)
                    return statsAndResources[i];
            return null;
        }

        protected static CharacterResource GetResource(BaseCharacter character, StatName resource)
        {
            for (var i = character.CharacterResources.Length; i-- > 0;)
                if (character.CharacterResources[i].Stat == resource)
                    return character.CharacterResources[i];
            return null;
        }

        public static float GetStatValue(BaseCharacter character, StatName stat) => GetStat(character, stat).TotalValue;
    }
}
