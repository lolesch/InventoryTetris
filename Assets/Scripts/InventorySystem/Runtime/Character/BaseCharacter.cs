using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Utility.Extensions;
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
        [field: SerializeField] public bool UseResource { get; set; } = true;

        [field: SerializeField] public CharacterStat[] CharacterStats { get; protected set; }
        [field: SerializeField] public CharacterResource[] CharacterResources { get; protected set; }

        [field: SerializeField, Range(1, 100)] public uint CharacterLevel { get; protected set; } = 1;

        //public event Action OnBlock;

        protected void OnValidate() => ResetStatsAndResources();

        protected void Update()
        {
            //TODO: COMBAT TICK RATE
            //var interval = 0f;
            //interval += Time.deltaTime;
            //if(interval >= combatTickRate)

            RegenerateHealth();
            RegenerateResource();
        }

        private void RegenerateHealth()
        {
            var health = GetResource(this, StatName.Health);

            if (health.IsDepleted || health.CurrentValue == health.TotalValue)
                return;

            health.AddToCurrent(GetStatValue(this, StatName.HealthRegeneration) * Time.deltaTime);
        }

        private void RegenerateResource()
        {
            var resource = GetResource(this, StatName.Resource);

            if (resource.CurrentValue == resource.TotalValue)
                return;

            resource.AddToCurrent(GetStatValue(this, StatName.ResourceRegeneration) * Time.deltaTime);
        }

        private void ResetStatsAndResources()
        {
            var resourcesOnly = new List<StatName>() { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience };

            var statNames = System.Enum.GetValues(typeof(StatName)) as StatName[];
            var statsOnly = statNames.ToList();

            foreach (var resource in resourcesOnly)
                statsOnly.Remove(resource);

            if (CharacterStats.Length != statsOnly.Count)
            {
                CharacterStats = new CharacterStat[statsOnly.Count];

                for (var i = 0; i < statsOnly.Count; i++)
                    CharacterStats[i] = new CharacterStat(statsOnly[i], 1);
            }

            if (CharacterResources.Length != resourcesOnly.Count)
            {
                CharacterResources = new CharacterResource[] {
                    new CharacterResource(StatName.Health, 100),
                    new CharacterResource(StatName.Resource, 60),
                    new CharacterResource(StatName.Shield, 0),
                    new CharacterResource(StatName.Experience, 0),
                };

                //for (var i = 0; i < resourcesOnly.Count; i++)
                //    CharacterResources[i] = new CharacterResource(resourcesOnly[i], 100);
            }
        }

        protected void Start()
        {
            GetResource(this, StatName.Health).CurrentHasDepleted -= OnDeath;
            GetResource(this, StatName.Health).CurrentHasDepleted += OnDeath;

            GetResource(this, StatName.Resource).CurrentHasDepleted -= CharacterResourceWarning;
            GetResource(this, StatName.Resource).CurrentHasDepleted += CharacterResourceWarning;

            //TODO: design Shield recharge
            GetResource(this, StatName.Shield).DepleteCurrent();

            //TODO: design Experience
            GetResource(this, StatName.Experience).DepleteCurrent();

            void CharacterResourceWarning() => Debug.LogWarning($"{name.ColoredComponent()} resource {"depleted".Colored(Color.red)}", this);
        }

        protected abstract void OnDeath();

        // just for testing
        protected float CalculateRequiredResource(BaseCharacter character, DamageType damageType)
        {
            if (!UseResource)
                return 0;

            var resource = GetStatValue(character, StatName.Resource);
            var damageTypeMod = damageType switch
            {
                DamageType.PhysicalDamage => .03f,
                DamageType.MagicalDamage => .1f,

                _ => 0f,
            };

            return resource * damageTypeMod;
        }

        protected static float CalculateDamageOutput(BaseCharacter character, DamageType damageType)
        {
            // TODO: if attackSpeed has only percantMods and a base of 0 it will return 0 
            var attackSpeed = GetStatValue(character, StatName.AttackSpeed);
            var damageTypeMod = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(character, StatName.PhysicalDamage),
                DamageType.MagicalDamage => GetStatValue(character, StatName.MagicalDamage),

                _ => 0f,
            };

            return damageTypeMod * (1f + attackSpeed * 0.01f); // * (1f + damageTypeMod * 0.01f);
        }

        protected static float CalculateReceivingDamage(BaseCharacter character, DamageType damageType, float incomingDamage)
        {
            if (character.IsInvincible)
                return 0f;

            var damageTypeResist = damageType switch
            {
                DamageType.PhysicalDamage => GetStatValue(character, StatName.Armor),
                DamageType.MagicalDamage => GetStatValue(character, StatName.MagicResist),

                _ => 0f,
            };

            // TODO: Desing defenses
            // Avoidance (block, dodge, barrier absorbtion)
            // Mitigation (resistances, armor)
            // Recovery

            var mitigatedDamage = incomingDamage * (1f - damageTypeResist * 0.01f);

            return mitigatedDamage;
        }

        public void DealDamageTo(BaseCharacter target, DamageType damageType)
        {
            var resource = GetResource(this, StatName.Resource);
            var resourceCost = CalculateRequiredResource(this, damageType);

            // TODO: if Shield protects resource
            //var shield = GetResource(this, StatName.Shield);

            //resourceCost = shield.RemoveFromCurrent(resourceCost);
            if (resourceCost == 0 || CanSpendResource(resource, resourceCost))
            {
                resource.RemoveFromCurrent(resourceCost);

                var damageOutput = CalculateDamageOutput(this, damageType);

                Debug.Log($"{name.ColoredComponent()} deals {damageOutput.ToString().Colored(Color.red)} {damageType}", this);
                target.ReceiveDamageFrom(this, damageType, damageOutput);
            }

            //AddDealtDPS(damageOutput);
        }

        public void ReceiveDamageFrom(BaseCharacter dealer, DamageType damageType, float incomingDamage)
        {
            var health = GetResource(this, StatName.Health);

            if (health.IsDepleted)
                return;

            var mitigatedDamage = CalculateReceivingDamage(this, damageType, incomingDamage);

            // TODO: if Shield protects resource instead => then skip shielding
            var shield = GetResource(this, StatName.Shield);

            var unshieldedDamage = shield.RemoveFromCurrent(mitigatedDamage);

            Debug.Log($"{name.ColoredComponent()} absorbs {Mathf.Min(mitigatedDamage - unshieldedDamage, shield.TotalValue).ToString().Colored(Color.red)} {damageType}", this);

            Debug.Log($"{name.ColoredComponent()} receives {Mathf.Min(unshieldedDamage, health.TotalValue).ToString().Colored(Color.red)} {damageType}", this);

            health.RemoveFromCurrent(unshieldedDamage);

            //AddReceivedDPS(healthDamage);
        }

        private bool CanSpendResource(CharacterResource resource, float amount) => amount <= resource.CurrentValue;

        public static CharacterStat GetStat(BaseCharacter character, StatName stat)
        {
            var statsAndResources = character.CharacterStats.Union(character.CharacterResources).ToArray();
            for (var i = statsAndResources.Length; i-- > 0;)
                if (statsAndResources[i].Stat == stat)
                    return statsAndResources[i];
            return null;
        }
        public static CharacterResource GetResource(BaseCharacter character, StatName resource)
        {
            for (var i = character.CharacterResources.Length; i-- > 0;)
                if (character.CharacterResources[i].Stat == resource)
                    return character.CharacterResources[i];
            return null;
        }

        public static float GetStatValue(BaseCharacter character, StatName stat) => GetStat(character, stat).TotalValue;
    }
}
